using ChainBallot.PublicContracts;
using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ElectionProcessing.Api.Processing;

/// <summary>
/// Core pipeline for the Election Processing Component (Architecture.md § 3.6, "Outbound"):
/// consumes ballots from the message broker, validates structure and the canonical
/// <c>BallotNullifier</c> replay rule, batches ballot commitments into a Merkle tree, generates and
/// verifies a batching proof, anchors the batch on the canonical settlement ledger, and publishes
/// projection evidence to the Transparency &amp; Audit Component. Failures are isolated per election:
/// a blocked election's queue is paused for retry while other elections keep processing.
/// </summary>
public sealed class ProcessingCoordinator(
    IBallotQueueIngress queueIngress,
    IBallotQueueConsumer queueConsumer,
    IBatchValidationService batchValidationService,
    ITreeStrategy treeStrategy,
    IZkProofStrategy zkProofStrategy,
    IProofArtifactVerifier proofArtifactVerifier,
    ISettlementAnchor settlementAnchor,
    IProjectionEvidencePublisher projectionEvidencePublisher,
    IAuthoritativeDigestSource authoritativeDigestSource,
    ISystemClock clock,
    IOptions<ProcessingOptions> options,
    ILogger<ProcessingCoordinator> logger) : IProcessingCoordinator
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ElectionState> _states = new(StringComparer.Ordinal);

    /// <summary>
    /// Drains newly delivered ballots from the queue, then advances every election's batch state by
    /// one cycle: closing batches whose count/wait thresholds are met, validating/verifying/anchoring
    /// them, and publishing anchored-batch projection evidence. Intended to be invoked repeatedly by
    /// the hosted background worker.
    /// </summary>
    public async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var drained = await queueConsumer.DrainAsync(cancellationToken);
        if (drained.Count > 0)
        {
            lock (_sync)
            {
                foreach (var package in drained)
                {
                    var state = GetOrCreateState(package.ElectionId);
                    state.Pending.Add(package);
                    state.ActiveBatchStartedAtUtc ??= clock.UtcNow;
                }
            }
        }

        List<string> electionIds;
        lock (_sync)
        {
            electionIds = _states.Keys.ToList();
        }

        foreach (var electionId in electionIds)
        {
            BatchAnchorRequest? request = null;
            List<BallotSubmissionPackage>? acceptedBallots = null;
            try
            {
                lock (_sync)
                {
                    var state = GetOrCreateState(electionId);
                    if (state.IsPaused || state.IsBlocked || state.Pending.Count == 0)
                    {
                        continue;
                    }

                    var shouldFlushByCount = state.Pending.Count >= options.Value.MaxBallotsPerBatch;
                    var shouldFlushByTime = state.ActiveBatchStartedAtUtc.HasValue
                        && clock.UtcNow - state.ActiveBatchStartedAtUtc.Value >= TimeSpan.FromSeconds(options.Value.MaxBatchWaitSeconds);

                    if (!shouldFlushByCount && !shouldFlushByTime)
                    {
                        continue;
                    }

                    var ballotCount = Math.Min(state.Pending.Count, options.Value.MaxBallotsPerBatch);
                    var ballots = state.Pending.Take(ballotCount).ToList();
                    state.Pending.RemoveRange(0, ballotCount);
                    state.ActiveBatchStartedAtUtc = state.Pending.Count > 0 ? clock.UtcNow : null;

                    var validation = batchValidationService.Validate(
                        electionId,
                        ballots,
                        new BatchValidationContext(state.SeenNullifiers, state.SeenCommitments));
                    state.RejectedBallotCount += validation.Rejected.Count;

                    if (validation.Accepted.Count == 0)
                    {
                        state.TallyStateVersion++;
                        request = null;
                        acceptedBallots = null;
                        continue;
                    }

                    acceptedBallots = validation.Accepted.ToList();
                    state.AcceptedBallotCount += acceptedBallots.Count;

                    var batchSequence = state.LastBatchSequence + 1;
                    var batchId = $"{electionId}-batch-{batchSequence:D6}";
                    var commitments = acceptedBallots.Select(x => x.BallotCommitment).ToArray();
                    var tree = treeStrategy.BuildRoot(commitments);
                    var batchInput = new BatchComputationInput(
                        ElectionId: electionId,
                        BatchId: batchId,
                        BatchSequence: batchSequence,
                        Commitments: commitments,
                        MerkleRoot: tree.Root,
                        CreatedAtUtc: clock.UtcNow);

                    var proofTask = zkProofStrategy.GenerateProofAsync(batchInput, cancellationToken);

                    var proof = proofTask.GetAwaiter().GetResult();

                    request = new BatchAnchorRequest(
                        ElectionId: electionId,
                        BatchId: batchId,
                        BatchSequence: batchSequence,
                        BallotCount: acceptedBallots.Count,
                        BatchRoot: tree.Root,
                        ProofArtifact: proof,
                        RequestedAtUtc: clock.UtcNow);

                    var verificationTask = proofArtifactVerifier.VerifyAsync(batchInput, proof, cancellationToken);
                    var verificationResult = verificationTask.GetAwaiter().GetResult();
                    if (!verificationResult.IsValid)
                    {
                        throw new InvalidOperationException(verificationResult.FailureReason ?? "Proof verification failed.");
                    }
                }

                if (request is null)
                {
                    continue;
                }

                var settlement = await settlementAnchor.AnchorAsync(request, cancellationToken);
                var metadataDigest = await authoritativeDigestSource.GetMetadataDigestAsync(electionId, cancellationToken);
                var auditDigest = await authoritativeDigestSource.GetAuditDigestAsync(electionId, cancellationToken);
                ProjectionEvidenceBatch? evidence = null;

                lock (_sync)
                {
                    var state = GetOrCreateState(electionId);
                    state.LastBatchSequence = request.BatchSequence;
                    state.FailedBatch = null;
                    state.IsBlocked = false;
                    state.BlockReason = null;
                    if (acceptedBallots is not null)
                    {
                        foreach (var ballot in acceptedBallots)
                        {
                            state.SeenNullifiers.Add(ballot.BallotNullifier);
                            state.SeenCommitments.Add(ballot.BallotCommitment);
                        }

                        state.AnchoredBallotCount += acceptedBallots.Count;
                    }

                    state.TallyStateVersion++;
                    state.LastAnchoredBatch = new BatchAnchorReference(
                        ElectionId: request.ElectionId,
                        BatchId: request.BatchId,
                        BatchSequence: request.BatchSequence,
                        BallotCount: request.BallotCount,
                        BatchRoot: request.BatchRoot,
                        ProofSystem: request.ProofArtifact.ProofSystem,
                        ProofReference: request.ProofArtifact.ProofReference,
                        SettlementReference: settlement,
                        AnchoredAtUtc: clock.UtcNow);

                    if (acceptedBallots is not null)
                    {
                        evidence = BuildProjectionEvidenceBatch(
                            request,
                            acceptedBallots,
                            settlement,
                            state,
                            options.Value.ProjectionPublishing.LifecycleState,
                            clock.UtcNow,
                            metadataDigest,
                            auditDigest);
                    }
                }

                if (evidence is not null)
                {
                    try
                    {
                        await projectionEvidencePublisher.PublishBatchAnchoredAsync(evidence, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Projection evidence publish failed for election {ElectionId} batch {BatchId}", request.ElectionId, request.BatchId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing for election {ElectionId}", electionId);

                if (request is null)
                {
                    continue;
                }

                lock (_sync)
                {
                    var state = GetOrCreateState(electionId);
                    state.IsBlocked = true;
                    state.BlockReason = ex.Message;
                    state.FailedBatch = new BatchFailure
                    {
                        AnchorRequest = request,
                        AcceptedBallots = acceptedBallots?.ToArray() ?? Array.Empty<BallotSubmissionPackage>(),
                        BatchId = request.BatchId,
                        BatchSequence = request.BatchSequence,
                        BallotCount = request.BallotCount,
                        FailureMessage = ex.Message,
                        FailedAtUtc = clock.UtcNow
                    };
                }
            }
        }
    }

    /// <summary>
    /// Validates the package synchronously (structure, canonical nullifier derivation, and duplication
    /// against already-anchored ballots) before it is forwarded to the broker, so malformed input is
    /// rejected with an explicit reason instead of being silently dropped later during batch closing.
    /// Cross-batch/within-batch duplicate detection among still-pending (not yet anchored) ballots
    /// remains the responsibility of the batch-closing validation in <see cref="RunCycleAsync"/>.
    /// </summary>
    public async Task<BallotIngestResult> EnqueueAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
    {
        string? rejectionReason;

        lock (_sync)
        {
            var state = GetOrCreateState(package.ElectionId);
            var validation = batchValidationService.Validate(
                package.ElectionId,
                new[] { package },
                new BatchValidationContext(state.SeenNullifiers, state.SeenCommitments));

            rejectionReason = validation.Accepted.Count == 0
                ? (validation.Rejected.Count > 0 ? validation.Rejected[0].Reason : "Ballot rejected.")
                : null;

            if (rejectionReason is not null)
            {
                state.RejectedBallotCount++;
            }
        }

        if (rejectionReason is not null)
        {
            return new BallotIngestResult(false, rejectionReason);
        }

        await queueIngress.EnqueueAsync(package, cancellationToken);
        return new BallotIngestResult(true, null);
    }

    public ProcessingStatusSnapshot GetStatus()
    {
        lock (_sync)
        {
            var elections = _states.Values
                .OrderBy(x => x.ElectionId, StringComparer.Ordinal)
                .Select(MapElectionState)
                .ToList();

            return new ProcessingStatusSnapshot(
                GeneratedAtUtc: clock.UtcNow,
                QueueDepth: queueConsumer.ApproximateDepth,
                ElectionCount: elections.Count,
                Elections: elections);
        }
    }

    public ElectionProcessingSnapshot GetElectionState(string electionId)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(electionId, out var state))
            {
                return new ElectionProcessingSnapshot(
                    ElectionId: electionId,
                    IsPaused: false,
                    IsBlocked: false,
                    BlockReason: null,
                    PendingBallotCount: 0,
                    AcceptedBallotCount: 0,
                    RejectedBallotCount: 0,
                    AnchoredBallotCount: 0,
                    TallyStateVersion: 0,
                    ActiveBatchStartedAtUtc: null,
                    FailedBatch: null,
                    LastAnchoredBatch: null);
            }

            return MapElectionState(state);
        }
    }

    public bool PauseElection(string electionId)
    {
        lock (_sync)
        {
            var state = GetOrCreateState(electionId);
            var changed = !state.IsPaused;
            state.IsPaused = true;
            return changed;
        }
    }

    public bool ResumeElection(string electionId)
    {
        lock (_sync)
        {
            var state = GetOrCreateState(electionId);
            var changed = state.IsPaused;
            state.IsPaused = false;
            return changed;
        }
    }

    /// <summary>
    /// Retries a blocked election's failed batch. CRITICAL INVARIANT: a batch whose proof fails
    /// verification must never reach <see cref="ISettlementAnchor"/>, including after one or more
    /// retries — this re-runs the same integrity/eligibility/proof checks the normal path performs
    /// before ever invoking settlement, and is safe under concurrent/duplicate retry requests (the
    /// failed batch is atomically claimed up front, so at most one concurrent retry can proceed).
    /// </summary>
    public async Task<BatchRetryOutcome> RetryFailedBatchAsync(string electionId, CancellationToken cancellationToken)
    {
        BatchFailure? failedBatch;

        lock (_sync)
        {
            var state = GetOrCreateState(electionId);
            failedBatch = state.FailedBatch;
            if (failedBatch is null)
            {
                return BatchRetryOutcome.NoFailedBatch;
            }

            // Atomic claim: no other concurrent retry can observe this same failed batch.
            state.FailedBatch = null;
        }

        var batchInput = new BatchComputationInput(
            ElectionId: failedBatch.AnchorRequest.ElectionId,
            BatchId: failedBatch.AnchorRequest.BatchId,
            BatchSequence: failedBatch.AnchorRequest.BatchSequence,
            Commitments: failedBatch.AcceptedBallots.Select(b => b.BallotCommitment).ToArray(),
            MerkleRoot: failedBatch.AnchorRequest.BatchRoot,
            CreatedAtUtc: failedBatch.AnchorRequest.RequestedAtUtc);

        var verificationResult = await proofArtifactVerifier.VerifyAsync(batchInput, failedBatch.AnchorRequest.ProofArtifact, cancellationToken);
        if (!verificationResult.IsValid)
        {
            // Restore the claim: the batch remains blocked and retryable, but settlement is never invoked.
            lock (_sync)
            {
                var state = GetOrCreateState(electionId);
                state.IsBlocked = true;
                state.BlockReason = verificationResult.FailureReason ?? "Proof verification failed on retry.";
                state.FailedBatch = failedBatch;
            }

            logger.LogWarning(
                "Retry rejected for election {ElectionId} batch {BatchId}: proof re-verification failed ({Reason}).",
                electionId, failedBatch.BatchId, verificationResult.FailureReason);
            return BatchRetryOutcome.ProofVerificationFailed;
        }

        var settlement = await settlementAnchor.AnchorAsync(failedBatch.AnchorRequest, cancellationToken);
        var metadataDigest = await authoritativeDigestSource.GetMetadataDigestAsync(electionId, cancellationToken);
        var auditDigest = await authoritativeDigestSource.GetAuditDigestAsync(electionId, cancellationToken);
        ProjectionEvidenceBatch? evidence = null;

        lock (_sync)
        {
            var state = GetOrCreateState(electionId);
            state.LastBatchSequence = Math.Max(state.LastBatchSequence, failedBatch.BatchSequence);
            state.IsBlocked = false;
            state.BlockReason = null;
            state.FailedBatch = null;
            foreach (var ballot in failedBatch.AcceptedBallots)
            {
                state.SeenNullifiers.Add(ballot.BallotNullifier);
                state.SeenCommitments.Add(ballot.BallotCommitment);
            }

            state.AnchoredBallotCount += failedBatch.AcceptedBallots.Count;
            state.TallyStateVersion++;
            state.LastAnchoredBatch = new BatchAnchorReference(
                ElectionId: failedBatch.AnchorRequest.ElectionId,
                BatchId: failedBatch.AnchorRequest.BatchId,
                BatchSequence: failedBatch.AnchorRequest.BatchSequence,
                BallotCount: failedBatch.AnchorRequest.BallotCount,
                BatchRoot: failedBatch.AnchorRequest.BatchRoot,
                ProofSystem: failedBatch.AnchorRequest.ProofArtifact.ProofSystem,
                ProofReference: failedBatch.AnchorRequest.ProofArtifact.ProofReference,
                SettlementReference: settlement,
                AnchoredAtUtc: clock.UtcNow);

            evidence = BuildProjectionEvidenceBatch(
                failedBatch.AnchorRequest,
                failedBatch.AcceptedBallots,
                settlement,
                state,
                options.Value.ProjectionPublishing.LifecycleState,
                clock.UtcNow,
                metadataDigest,
                auditDigest);
        }

        if (evidence is not null)
        {
            try
            {
                await projectionEvidencePublisher.PublishBatchAnchoredAsync(evidence, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Projection evidence publish failed while retrying election {ElectionId} batch {BatchId}", failedBatch.AnchorRequest.ElectionId, failedBatch.AnchorRequest.BatchId);
            }
        }

        return BatchRetryOutcome.Retried;
    }

    private ElectionState GetOrCreateState(string electionId)
    {
        if (!_states.TryGetValue(electionId, out var state))
        {
            state = new ElectionState
            {
                ElectionId = electionId,
                Pending = new List<BallotSubmissionPackage>(),
                IsPaused = false,
                IsBlocked = false,
                BlockReason = null,
                ActiveBatchStartedAtUtc = null,
                LastBatchSequence = 0,
                FailedBatch = null,
                LastAnchoredBatch = null,
                SeenNullifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                SeenCommitments = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                AcceptedBallotCount = 0,
                RejectedBallotCount = 0,
                AnchoredBallotCount = 0,
                TallyStateVersion = 0
            };
            _states[electionId] = state;
        }

        return state;
    }

    private static ElectionProcessingSnapshot MapElectionState(ElectionState state)
    {
        return new ElectionProcessingSnapshot(
            ElectionId: state.ElectionId,
            IsPaused: state.IsPaused,
            IsBlocked: state.IsBlocked,
            BlockReason: state.BlockReason,
            PendingBallotCount: state.Pending.Count,
            AcceptedBallotCount: state.AcceptedBallotCount,
            RejectedBallotCount: state.RejectedBallotCount,
            AnchoredBallotCount: state.AnchoredBallotCount,
            TallyStateVersion: state.TallyStateVersion,
            ActiveBatchStartedAtUtc: state.ActiveBatchStartedAtUtc,
            FailedBatch: state.FailedBatch is null
                ? null
                : new FailedBatchSnapshot(
                    BatchId: state.FailedBatch.BatchId,
                    BatchSequence: state.FailedBatch.BatchSequence,
                    BallotCount: state.FailedBatch.BallotCount,
                    FailureMessage: state.FailedBatch.FailureMessage,
                    FailedAtUtc: state.FailedBatch.FailedAtUtc),
            LastAnchoredBatch: state.LastAnchoredBatch);
    }

    private static ProjectionEvidenceBatch BuildProjectionEvidenceBatch(
        BatchAnchorRequest request,
        IReadOnlyList<BallotSubmissionPackage> acceptedBallots,
        SettlementReference settlement,
        ElectionState state,
        string lifecycleState,
        DateTimeOffset generatedAtUtc,
        string? metadataDigest,
        string? auditDigest)
    {
        var vidEntries = acceptedBallots
            .Select((ballot, index) => new ProjectionVidEvidence(
                Vid: ballot.Vid,
                BatchId: request.BatchId,
                BallotCommitment: ballot.BallotCommitment,
                InclusionProofReference: new InclusionProofReference(
                    BatchId: request.BatchId,
                    MerkleRoot: request.BatchRoot,
                    LeafCommitment: ballot.BallotCommitment,
                    LeafIndex: index,
                    ProofUri: request.ProofArtifact.ProofReference,
                    ProofHash: request.ProofArtifact.ProofDigest),
                RecordedAtUtc: generatedAtUtc,
                ConsistencyDigest: null))
            .ToArray();

        var tallies = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["acceptedBallots"] = state.AcceptedBallotCount,
            ["rejectedBallots"] = state.RejectedBallotCount,
            ["anchoredBallots"] = state.AnchoredBallotCount
        };

        return new ProjectionEvidenceBatch(
            ElectionId: request.ElectionId,
            LifecycleState: lifecycleState,
            ResultDigest: ComputeResultDigest(request, settlement, state.TallyStateVersion),
            SettlementReference: settlement,
            CandidateTallies: tallies,
            VidEntries: vidEntries,
            GeneratedAtUtc: generatedAtUtc,
            MetadataDigest: metadataDigest,
            AuditDigest: auditDigest);
    }

    private static string ComputeResultDigest(BatchAnchorRequest request, SettlementReference settlement, int tallyStateVersion)
    {
        var input = $"{request.ElectionId}|{request.BatchId}|{request.BatchRoot}|{settlement.TransactionSignature}|{tallyStateVersion}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}

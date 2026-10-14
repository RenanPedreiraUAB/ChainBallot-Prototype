using System.Security.Claims;
using ChainBallot.PublicContracts;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Default implementation of <see cref="IVoterWorkflowService"/> — the Voter Component's realization
/// of UIS-01/UIS-02 (Architecture.md § 3.4). Coordinates the Election Authorization Component (VID,
/// eligibility, grant issue/consume), election metadata, ballot encryption/commitment, and the durable
/// submission outbox, retaining packages for retry when an attempted broker publication fails.
/// </summary>
public sealed class VoterWorkflowService(
    IHttpContextAccessor httpContextAccessor,
    IElectionAuthorizationClient authorizationClient,
    IElectionMetadataClient metadataClient,
    IBallotEncryptionService ballotEncryptionService,
    IBallotCommitmentService ballotCommitmentService,
    IQueuePublisher queuePublisher,
    IBallotSubmissionOutbox submissionOutbox,
    IOptions<VoterOptions> options,
    ILogger<VoterWorkflowService> logger) : IVoterWorkflowService
{
    /// <inheritdoc />
    public async Task<VoterDashboardState> LoadDashboardAsync(string electionId, CancellationToken cancellationToken)
    {
        await RetryPendingSubmissionsAsync(cancellationToken);

        var normalizedElectionId = string.IsNullOrWhiteSpace(electionId)
            ? options.Value.DefaultElectionId
            : electionId.Trim();

        var profile = await authorizationClient.GetProfileAsync(cancellationToken);
        var state = await authorizationClient.GetElectionStateAsync(normalizedElectionId, cancellationToken);
        var metadata = await metadataClient.GetElectionMetadataAsync(normalizedElectionId, cancellationToken);
        var candidateOptions = metadata?.Candidates
            .Select(candidate => new CandidateOption(candidate.Id, candidate.Name, candidate.Party, candidate.Description))
            .ToArray()
            ?? Array.Empty<CandidateOption>();

        var selectedCandidate = candidateOptions.FirstOrDefault();

        return new VoterDashboardState(
            ElectionId: normalizedElectionId,
            Profile: profile,
            AuthorizationState: state,
            Metadata: metadata,
            CandidateOptions: candidateOptions,
            StatusMessage: state?.Status switch
            {
                ElectionAuthorizationStatus.AlreadyVoted => "This VID has already consumed its authorization.",
                ElectionAuthorizationStatus.NotEligible => "This voter is not eligible for the selected election.",
                _ => null
            },
            CanVote: state?.Status == ElectionAuthorizationStatus.EligibleNotVoted,
            SelectedCandidateName: selectedCandidate?.Name,
            SelectedCandidateId: selectedCandidate?.Id,
            MetadataEndpoint: BuildMetadataEndpoint(normalizedElectionId),
            LastQueueReceipt: null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// SECURITY INVARIANT: the ballot package is durably staged in the submission outbox BEFORE the
    /// one-time authorization grant is consumed (Architecture.md § 3.4). This makes durable staging part
    /// of the normal submission path, not merely a fallback taken after a publish failure, so a crash at
    /// any point after staging still leaves a recoverable record and no voting right is ever consumed
    /// without one.
    /// </remarks>
    public async Task<VoterSubmissionOutcome> SubmitBallotAsync(BallotSelectionRequest request, CancellationToken cancellationToken)
    {
        await RetryPendingSubmissionsAsync(cancellationToken);

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal is null || principal.Identity?.IsAuthenticated != true)
        {
            return new VoterSubmissionOutcome(false, "You must sign in before submitting a ballot.", null, null, null, null);
        }

        var subject = ResolveSubject(principal);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return new VoterSubmissionOutcome(false, "Unable to resolve the authenticated subject.", null, null, null, null);
        }

        var profile = await authorizationClient.GetProfileAsync(cancellationToken);
        var state = await authorizationClient.GetElectionStateAsync(request.ElectionId, cancellationToken);
        if (profile is null || state is null)
        {
            return new VoterSubmissionOutcome(false, "Authorization state could not be loaded.", null, null, null, null);
        }

        if (state.Status != ElectionAuthorizationStatus.EligibleNotVoted)
        {
            return new VoterSubmissionOutcome(false, $"Vote cannot proceed because the election state is {state.Status}.", null, null, null, null);
        }

        var metadata = await metadataClient.GetElectionMetadataAsync(request.ElectionId, cancellationToken);
        var selectedCandidateName = ResolveCandidateName(request.CandidateId, request.CandidateName, metadata);

        if (string.IsNullOrWhiteSpace(selectedCandidateName))
        {
            return new VoterSubmissionOutcome(false, "A candidate must be selected before submission.", null, null, null, null);
        }

        var grant = await authorizationClient.IssueGrantAsync(request.ElectionId, cancellationToken);
        if (grant is null)
        {
            return new VoterSubmissionOutcome(false, "A voting authorization grant could not be issued.", null, null, null, null);
        }

        var submissionNonce = Guid.NewGuid().ToString("N");

        var draft = new BallotSubmissionDraft(
            ElectionId: request.ElectionId,
            Subject: subject,
            Vid: profile.Vid,
            CandidateId: request.CandidateId,
            CandidateName: selectedCandidateName,
            MetadataHash: metadata?.Verification.MetadataHash ?? string.Empty,
            SubmissionNonce: submissionNonce,
            BallotNullifier: ComputeNullifier(request.ElectionId, profile.Vid, submissionNonce),
            SubmittedAtUtc: DateTimeOffset.UtcNow);

        var encryptedPayload = ballotEncryptionService.Encrypt(draft);
        var commitment = ballotCommitmentService.ComputeCommitment(draft, encryptedPayload);
        var package = new BallotSubmissionPackage(
            ElectionId: request.ElectionId,
            Vid: profile.Vid,
            BallotCommitment: commitment,
            SubmissionNonce: draft.SubmissionNonce,
            BallotNullifier: draft.BallotNullifier,
            EncryptedVotePayload: encryptedPayload,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        // Durable staging must complete before the grant is consumed: see the security invariant above.
        try
        {
            submissionOutbox.Stage(package, grant.GrantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to durably stage ballot submission for election {ElectionId}; authorization grant was not consumed.", request.ElectionId);
            return new VoterSubmissionOutcome(false, "The ballot could not be durably staged, so authorization was not consumed. Please retry.", grant, null, null, package);
        }

        var consumeResponse = await authorizationClient.ConsumeGrantAsync(request.ElectionId, grant.GrantId, draft.SubmissionNonce, cancellationToken);

        if (consumeResponse is null || consumeResponse.Outcome != AuthorizationConsumeOutcome.Consumed)
        {
            // Authorization was never granted for this staged package, so it must never be published.
            submissionOutbox.TryRemove(package.ElectionId, package.BallotNullifier, out _);
            return new VoterSubmissionOutcome(false, consumeResponse?.Message ?? "The authorization grant could not be consumed.", grant, consumeResponse, null, package);
        }

        // Grant consumed: flip the durable record before attempting to publish, so a crash here is still recoverable.
        submissionOutbox.MarkAuthorized(package.ElectionId, package.BallotNullifier);

        try
        {
            var queueReceipt = await queuePublisher.PublishAsync(package, cancellationToken);
            submissionOutbox.TryRemove(package.ElectionId, package.BallotNullifier, out _);
            return new VoterSubmissionOutcome(true, "Ballot has been encrypted, queued, and authorized.", grant, consumeResponse, queueReceipt, package);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Queue publish failed for election {ElectionId}; ballot remains staged for retry.", request.ElectionId);
            return new VoterSubmissionOutcome(false, "Authorization was consumed, but queue submission failed. The ballot has been retained for retry.", grant, consumeResponse, null, package);
        }
    }

    private static string ResolveSubject(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? string.Empty;
    }

    private static string? ResolveCandidateName(int candidateId, string? candidateName, ElectionMetadataDocument? metadata)
    {
        if (!string.IsNullOrWhiteSpace(candidateName))
        {
            return candidateName.Trim();
        }

        return metadata?.Candidates.FirstOrDefault(candidate => candidate.Id == candidateId)?.Name;
    }

    private string? BuildMetadataEndpoint(string electionId)
    {
        var template = options.Value.MetadataEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return template.Replace("{electionId}", electionId, StringComparison.Ordinal);
    }

    private static string ComputeNullifier(string electionId, string vid, string submissionNonce)
    {
        var input = $"{electionId}|{vid}|{submissionNonce}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task RetryPendingSubmissionsAsync(CancellationToken cancellationToken)
    {
        var outboxTtl = TimeSpan.FromMinutes(Math.Max(1, options.Value.SubmissionOutboxEntryTtlMinutes));
        var outboxMaxRetryCount = Math.Max(0, options.Value.SubmissionOutboxMaxRetryCount);
        var outboxMaxEntries = Math.Max(1, options.Value.SubmissionOutboxMaxEntries);

        submissionOutbox.Compact(DateTimeOffset.UtcNow, outboxTtl, outboxMaxRetryCount, outboxMaxEntries);

        while (submissionOutbox.TryTakeNext(out var pendingSubmission))
        {
            if (pendingSubmission.Status == SubmissionStatus.Staged)
            {
                if (pendingSubmission.GrantId is not { } grantId)
                {
                    // No recoverable grant reference: this staged package can never be authorized.
                    continue;
                }

                var consumeResponse = await authorizationClient.ConsumeGrantAsync(
                    pendingSubmission.Package.ElectionId, grantId, pendingSubmission.Package.SubmissionNonce, cancellationToken);

                if (consumeResponse is null || consumeResponse.Outcome != AuthorizationConsumeOutcome.Consumed)
                {
                    // Authorization can never be granted for this staged package; it must never be published.
                    logger.LogWarning("Recovered staged submission for election {ElectionId} could not be authorized: {Outcome}.", pendingSubmission.Package.ElectionId, consumeResponse?.Outcome);
                    continue;
                }

                pendingSubmission = pendingSubmission with { Status = SubmissionStatus.AuthorizedPendingPublish };
            }

            try
            {
                await queuePublisher.PublishAsync(pendingSubmission.Package, cancellationToken);
            }
            catch (Exception ex)
            {
                submissionOutbox.Requeue(pendingSubmission, $"retry failed: {ex.Message}");
                break;
            }
        }
    }
}
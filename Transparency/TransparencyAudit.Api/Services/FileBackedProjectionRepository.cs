using System.Text.Json;
using ChainBallot.PublicContracts;
using TransparencyAudit.Api.Domain;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// Default, file-backed implementation of <see cref="IProjectionRepository"/> — the Transparency &amp;
/// Audit Component's read-model store for this prototype (Prototype.md § 4.8).
/// </summary>
public sealed class FileBackedProjectionRepository : IProjectionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _projectionStorePath;
    private readonly IConsistencyDigestCalculator _digestCalculator;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private ProjectionStoreSnapshot? _cachedSnapshot;

    public FileBackedProjectionRepository(string projectionStorePath, IConsistencyDigestCalculator digestCalculator)
    {
        if (string.IsNullOrWhiteSpace(projectionStorePath))
        {
            throw new ArgumentException("Projection store path is required.", nameof(projectionStorePath));
        }

        _projectionStorePath = projectionStorePath;
        _digestCalculator = digestCalculator;
    }

    public async Task<ElectionProjection?> GetElectionProjectionAsync(string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return null;
        }

        var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return snapshot.Elections
            .FirstOrDefault(e => string.Equals(e.ElectionId, electionId, StringComparison.OrdinalIgnoreCase))
            ?.ToProjection();
    }

    public async Task<VidEntry?> GetVidEntryAsync(string electionId, string vid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId) || string.IsNullOrWhiteSpace(vid))
        {
            return null;
        }

        var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var election = snapshot.Elections.FirstOrDefault(e => string.Equals(e.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
        if (election is null)
        {
            return null;
        }

        return election.VidEntries
            .FirstOrDefault(v => string.Equals(v.Vid, vid, StringComparison.OrdinalIgnoreCase))
            ?.ToVidEntry(election.ElectionId);
    }

    public async Task<ElectionProjection> UpsertProjectionEvidenceAsync(ProjectionEvidenceIngestion evidence, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidence.ElectionId))
        {
            throw new ArgumentException("Election id is required.", nameof(evidence));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await EnsureSnapshotLoadedUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var records = snapshot.Elections.ToList();

            var vidEntries = evidence.VidEntries
                .Select(v =>
                {
                    var vidDigest = string.IsNullOrWhiteSpace(v.ConsistencyDigest)
                        ? _digestCalculator.ComputeVidDigest(
                            evidence.ElectionId,
                            v.Vid,
                            v.BatchId,
                            v.BallotCommitment,
                            v.InclusionProofReference.MerkleRoot,
                            v.InclusionProofReference.LeafIndex)
                        : v.ConsistencyDigest;

                    return new VidEntryRecord(
                        v.Vid,
                        v.BatchId,
                        v.BallotCommitment,
                        SettlementReferenceRecord.FromContract(evidence.SettlementReference),
                        InclusionProofReferenceRecord.FromContract(v.InclusionProofReference),
                        v.RecordedAtUtc,
                        vidDigest);
                })
                .ToArray();

            var digestInputEntries = vidEntries
                .Select(v => new VidEvidenceEntry(
                    v.Vid,
                    v.BatchId,
                    v.BallotCommitment,
                    v.InclusionProofReference.ToContract(),
                    v.RecordedAtUtc,
                    v.ConsistencyDigest))
                .ToArray();

            var consistencyDigest = _digestCalculator.ComputeElectionDigest(
                evidence.ElectionId,
                evidence.LifecycleState,
                evidence.ResultDigest,
                evidence.CandidateTallies,
                digestInputEntries,
                evidence.MetadataDigest,
                evidence.AuditDigest);

            var updated = new ElectionProjectionRecord(
                evidence.ElectionId,
                evidence.LifecycleState,
                evidence.ResultDigest,
                SettlementReferenceRecord.FromContract(evidence.SettlementReference),
                evidence.CandidateTallies,
                DateTimeOffset.UtcNow,
                vidEntries,
                consistencyDigest,
                evidence.MetadataDigest,
                evidence.AuditDigest,
                evidence.IsStale,
                evidence.StaleReason);

            var index = records.FindIndex(r => string.Equals(r.ElectionId, evidence.ElectionId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                records[index] = updated;
            }
            else
            {
                records.Add(updated);
            }

            _cachedSnapshot = new ProjectionStoreSnapshot(records);
            await PersistSnapshotUnsafeAsync(_cachedSnapshot, cancellationToken).ConfigureAwait(false);
            return updated.ToProjection();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ElectionProjection?> MarkElectionStaleAsync(string electionId, string staleReason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return null;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await EnsureSnapshotLoadedUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var records = snapshot.Elections.ToList();
            var index = records.FindIndex(r => string.Equals(r.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return null;
            }

            var target = records[index];
            records[index] = target with
            {
                IsStale = true,
                StaleReason = staleReason,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };

            _cachedSnapshot = new ProjectionStoreSnapshot(records);
            await PersistSnapshotUnsafeAsync(_cachedSnapshot, cancellationToken).ConfigureAwait(false);
            return records[index].ToProjection();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ElectionProjection?> ReindexElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return null;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await EnsureSnapshotLoadedUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var records = snapshot.Elections.ToList();
            var index = records.FindIndex(r => string.Equals(r.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return null;
            }

            var existing = records[index];
            var refreshedVidEntries = existing.VidEntries
                .Select(v =>
                {
                    var vidDigest = string.IsNullOrWhiteSpace(v.ConsistencyDigest)
                        ? _digestCalculator.ComputeVidDigest(
                            existing.ElectionId,
                            v.Vid,
                            v.BatchId,
                            v.BallotCommitment,
                            v.InclusionProofReference.MerkleRoot,
                            v.InclusionProofReference.LeafIndex)
                        : v.ConsistencyDigest;
                    return v with { ConsistencyDigest = vidDigest };
                })
                .ToArray();

            var digestInputEntries = refreshedVidEntries
                .Select(v => new VidEvidenceEntry(
                    v.Vid,
                    v.BatchId,
                    v.BallotCommitment,
                    v.InclusionProofReference.ToContract(),
                    v.RecordedAtUtc,
                    v.ConsistencyDigest))
                .ToArray();

            var refreshedDigest = _digestCalculator.ComputeElectionDigest(
                existing.ElectionId,
                existing.LifecycleState,
                existing.ResultDigest,
                existing.CandidateTallies,
                digestInputEntries,
                existing.MetadataDigest,
                existing.AuditDigest);

            var updated = existing with
            {
                VidEntries = refreshedVidEntries,
                ConsistencyDigest = refreshedDigest,
                IsStale = false,
                StaleReason = null,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };

            records[index] = updated;
            _cachedSnapshot = new ProjectionStoreSnapshot(records);
            await PersistSnapshotUnsafeAsync(_cachedSnapshot, cancellationToken).ConfigureAwait(false);
            return updated.ToProjection();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<ProjectionStoreSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_cachedSnapshot is not null)
            {
                return _cachedSnapshot;
            }

            var directory = Path.GetDirectoryName(_projectionStorePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _cachedSnapshot = await EnsureSnapshotLoadedUnsafeAsync(cancellationToken).ConfigureAwait(false);
            return _cachedSnapshot;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<ProjectionStoreSnapshot> EnsureSnapshotLoadedUnsafeAsync(CancellationToken cancellationToken)
    {
        if (_cachedSnapshot is not null)
        {
            return _cachedSnapshot;
        }

        var directory = Path.GetDirectoryName(_projectionStorePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_projectionStorePath))
        {
            _cachedSnapshot = new ProjectionStoreSnapshot(Array.Empty<ElectionProjectionRecord>());
            await PersistSnapshotUnsafeAsync(_cachedSnapshot, cancellationToken).ConfigureAwait(false);
            return _cachedSnapshot;
        }

        await using var stream = File.Open(_projectionStorePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var loaded = await JsonSerializer.DeserializeAsync<ProjectionStoreSnapshot>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        _cachedSnapshot = loaded ?? new ProjectionStoreSnapshot(Array.Empty<ElectionProjectionRecord>());
        return _cachedSnapshot;
    }

    private async Task PersistSnapshotUnsafeAsync(ProjectionStoreSnapshot snapshot, CancellationToken cancellationToken)
    {
        var tempPath = $"{_projectionStorePath}.tmp";
        await using (var stream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _projectionStorePath, overwrite: true);
    }

    internal sealed record ProjectionStoreSnapshot(IReadOnlyList<ElectionProjectionRecord> Elections);

    internal sealed record ElectionProjectionRecord(
        string ElectionId,
        string LifecycleState,
        string ResultDigest,
        SettlementReferenceRecord SettlementReference,
        IReadOnlyDictionary<string, long> CandidateTallies,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<VidEntryRecord> VidEntries,
        string? ConsistencyDigest = null,
        string? MetadataDigest = null,
        string? AuditDigest = null,
        bool IsStale = false,
        string? StaleReason = null)
    {
        public ElectionProjection ToProjection()
        {
            return new ElectionProjection(
                ElectionId,
                LifecycleState,
                SettlementReference.ToContract(),
                CandidateTallies,
                ResultDigest,
                GeneratedAtUtc,
                VidEntries.Select(v => v.ToVidEntry(ElectionId)).ToArray(),
                ConsistencyDigest,
                MetadataDigest,
                AuditDigest,
                IsStale,
                StaleReason);
        }
    }

    internal sealed record VidEntryRecord(
        string Vid,
        string BatchId,
        string BallotCommitment,
        SettlementReferenceRecord SettlementReference,
        InclusionProofReferenceRecord InclusionProofReference,
        DateTimeOffset RecordedAtUtc,
        string? ConsistencyDigest)
    {
        public VidEntry ToVidEntry(string electionId)
        {
            return new VidEntry(
                electionId,
                Vid,
                BatchId,
                BallotCommitment,
                SettlementReference.ToContract(),
                InclusionProofReference.ToContract(),
                RecordedAtUtc,
                ConsistencyDigest);
        }
    }

    internal sealed record SettlementReferenceRecord(
        string Network,
        string ProgramId,
        string ElectionId,
        string TransactionSignature,
        long Slot,
        DateTimeOffset? BlockTimeUtc,
        string Commitment)
    {
        public static SettlementReferenceRecord FromContract(SettlementReference source)
            => new(
                source.Network,
                source.ProgramId,
                source.ElectionId,
                source.TransactionSignature,
                checked((long)source.Slot),
                source.BlockTimeUtc,
                source.Commitment);

        public SettlementReference ToContract()
            => new(
                Network,
                ProgramId,
                ElectionId,
                TransactionSignature,
                checked((ulong)Slot),
                BlockTimeUtc,
                Commitment);
    }

    internal sealed record InclusionProofReferenceRecord(
        string BatchId,
        string MerkleRoot,
        string LeafCommitment,
        int LeafIndex,
        string? ProofUri,
        string? ProofHash)
    {
        public static InclusionProofReferenceRecord FromContract(InclusionProofReference source)
            => new(
                source.BatchId,
                source.MerkleRoot,
                source.LeafCommitment,
                source.LeafIndex,
                source.ProofUri,
                source.ProofHash);

        public InclusionProofReference ToContract()
            => new(
                BatchId,
                MerkleRoot,
                LeafCommitment,
                LeafIndex,
                ProofUri,
                ProofHash);
    }
}

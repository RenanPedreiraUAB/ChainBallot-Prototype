using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public sealed record ProcessingStatusSnapshot(
    DateTimeOffset GeneratedAtUtc,
    int QueueDepth,
    int ElectionCount,
    IReadOnlyList<ElectionProcessingSnapshot> Elections);

public sealed record ElectionProcessingSnapshot(
    string ElectionId,
    bool IsPaused,
    bool IsBlocked,
    string? BlockReason,
    int PendingBallotCount,
    long AcceptedBallotCount,
    long RejectedBallotCount,
    long AnchoredBallotCount,
    int TallyStateVersion,
    DateTimeOffset? ActiveBatchStartedAtUtc,
    FailedBatchSnapshot? FailedBatch,
    BatchAnchorReference? LastAnchoredBatch);

public sealed record FailedBatchSnapshot(
    string BatchId,
    int BatchSequence,
    int BallotCount,
    string FailureMessage,
    DateTimeOffset FailedAtUtc);

internal sealed class ElectionState
{
    public required string ElectionId { get; init; }

    public required List<BallotSubmissionPackage> Pending { get; init; }

    public bool IsPaused { get; set; }

    public bool IsBlocked { get; set; }

    public string? BlockReason { get; set; }

    public DateTimeOffset? ActiveBatchStartedAtUtc { get; set; }

    public int LastBatchSequence { get; set; }

    public BatchFailure? FailedBatch { get; set; }

    public BatchAnchorReference? LastAnchoredBatch { get; set; }

    public required HashSet<string> SeenNullifiers { get; init; }

    public required HashSet<string> SeenCommitments { get; init; }

    public long AcceptedBallotCount { get; set; }

    public long RejectedBallotCount { get; set; }

    public long AnchoredBallotCount { get; set; }

    public int TallyStateVersion { get; set; }
}

internal sealed class BatchFailure
{
    public required BatchAnchorRequest AnchorRequest { get; init; }

    public required IReadOnlyList<BallotSubmissionPackage> AcceptedBallots { get; init; }

    public required string BatchId { get; init; }

    public int BatchSequence { get; init; }

    public int BallotCount { get; init; }

    public required string FailureMessage { get; init; }

    public DateTimeOffset FailedAtUtc { get; init; }
}

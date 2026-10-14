using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public interface IProcessingCoordinator
{
    Task RunCycleAsync(CancellationToken cancellationToken);

    Task<BallotIngestResult> EnqueueAsync(BallotSubmissionPackage package, CancellationToken cancellationToken);

    ProcessingStatusSnapshot GetStatus();

    ElectionProcessingSnapshot GetElectionState(string electionId);

    bool PauseElection(string electionId);

    bool ResumeElection(string electionId);

    Task<BatchRetryOutcome> RetryFailedBatchAsync(string electionId, CancellationToken cancellationToken);
}

/// <summary>Outcome of a failed-batch retry attempt (Architecture.md § 3.6).</summary>
public enum BatchRetryOutcome
{
    /// <summary>No failed batch is currently pending for this election.</summary>
    NoFailedBatch,

    /// <summary>Proof re-verification failed; settlement was never invoked and the batch remains retryable.</summary>
    ProofVerificationFailed,

    /// <summary>All checks passed and the batch was settled.</summary>
    Retried
}

/// <summary>Outcome of a synchronous ingest-time validation check (Architecture.md § 3.6).</summary>
public sealed record BallotIngestResult(bool Accepted, string? RejectionReason);

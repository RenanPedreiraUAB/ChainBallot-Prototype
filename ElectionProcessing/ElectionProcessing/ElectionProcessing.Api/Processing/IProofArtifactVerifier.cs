namespace ElectionProcessing.Api.Processing;

public interface IProofArtifactVerifier
{
    string Name { get; }

    Task<ProofVerificationResult> VerifyAsync(
        BatchComputationInput input,
        ProofArtifact proofArtifact,
        CancellationToken cancellationToken);
}

public sealed record ProofVerificationResult(
    bool IsValid,
    string? FailureReason = null);

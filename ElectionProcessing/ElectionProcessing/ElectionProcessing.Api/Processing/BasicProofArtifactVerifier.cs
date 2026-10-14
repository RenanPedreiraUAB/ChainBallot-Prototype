namespace ElectionProcessing.Api.Processing;

public sealed class BasicProofArtifactVerifier : IProofArtifactVerifier
{
    public string Name => "BasicProofArtifactVerifier";

    public Task<ProofVerificationResult> VerifyAsync(
        BatchComputationInput input,
        ProofArtifact proofArtifact,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(proofArtifact.ProofSystem))
        {
            return Task.FromResult(new ProofVerificationResult(false, "ProofSystem is required."));
        }

        if (string.IsNullOrWhiteSpace(proofArtifact.ProofReference))
        {
            return Task.FromResult(new ProofVerificationResult(false, "ProofReference is required."));
        }

        if (string.IsNullOrWhiteSpace(proofArtifact.ProofDigest))
        {
            return Task.FromResult(new ProofVerificationResult(false, "ProofDigest is required."));
        }

        return Task.FromResult(new ProofVerificationResult(true));
    }
}

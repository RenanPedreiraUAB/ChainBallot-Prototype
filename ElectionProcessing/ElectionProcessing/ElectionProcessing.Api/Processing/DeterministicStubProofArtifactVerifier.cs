using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectionProcessing.Api.Application;

namespace ElectionProcessing.Api.Processing;

public sealed class DeterministicStubProofArtifactVerifier : IProofArtifactVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "DeterministicStubVerifier";

    public Task<ProofVerificationResult> VerifyAsync(
        BatchComputationInput input,
        ProofArtifact proofArtifact,
        CancellationToken cancellationToken)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            input.ElectionId,
            input.BatchId,
            input.BatchSequence,
            input.MerkleRoot,
            Commitments = input.Commitments.OrderBy(x => x, StringComparer.Ordinal).ToArray()
        }, JsonOptions);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var expectedDigest = Convert.ToHexString(digest).ToLowerInvariant();
        var expectedReference = $"stub://proofs/{input.ElectionId}/{input.BatchId}/{expectedDigest}";

        if (!string.Equals(proofArtifact.ProofSystem, ProverModes.DeterministicStub, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProofVerificationResult(false, "ProofSystem is not DeterministicStub."));
        }

        if (!string.Equals(proofArtifact.ProofDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProofVerificationResult(false, "Proof digest mismatch for deterministic stub prover."));
        }

        if (!string.Equals(proofArtifact.ProofReference, expectedReference, StringComparison.Ordinal))
        {
            return Task.FromResult(new ProofVerificationResult(false, "Proof reference mismatch for deterministic stub prover."));
        }

        return Task.FromResult(new ProofVerificationResult(true));
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ElectionProcessing.Api.Processing;

public sealed class DeterministicStubZkProofStrategy : IZkProofStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "DeterministicStub";

    public Task<ProofArtifact> GenerateProofAsync(BatchComputationInput input, CancellationToken cancellationToken)
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
        var digestHex = Convert.ToHexString(digest).ToLowerInvariant();

        var proof = new ProofArtifact(
            ProofSystem: Name,
            ProofReference: $"stub://proofs/{input.ElectionId}/{input.BatchId}/{digestHex}",
            ProofDigest: digestHex,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(proof);
    }
}

namespace ElectionProcessing.Api.Processing;

public interface IZkProofStrategy
{
    string Name { get; }

    Task<ProofArtifact> GenerateProofAsync(BatchComputationInput input, CancellationToken cancellationToken);
}

public sealed record BatchComputationInput(
    string ElectionId,
    string BatchId,
    int BatchSequence,
    IReadOnlyList<string> Commitments,
    string MerkleRoot,
    DateTimeOffset CreatedAtUtc);

public sealed record ProofArtifact(
    string ProofSystem,
    string ProofReference,
    string ProofDigest,
    DateTimeOffset CreatedAtUtc);

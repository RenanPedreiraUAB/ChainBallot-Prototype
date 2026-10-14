namespace ElectionProcessing.Api.Processing;

public interface ITreeStrategy
{
    string Name { get; }

    TreeComputationResult BuildRoot(IReadOnlyList<string> commitments);
}

public sealed record TreeComputationResult(
    string Root,
    int LeafCount,
    string DigestAlgorithm);

using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public interface ISettlementAnchor
{
    Task<SettlementReference> AnchorAsync(BatchAnchorRequest request, CancellationToken cancellationToken);
}

public sealed record BatchAnchorRequest(
    string ElectionId,
    string BatchId,
    int BatchSequence,
    int BallotCount,
    string BatchRoot,
    ProofArtifact ProofArtifact,
    DateTimeOffset RequestedAtUtc);

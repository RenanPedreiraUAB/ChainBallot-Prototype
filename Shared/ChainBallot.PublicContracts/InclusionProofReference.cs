namespace ChainBallot.PublicContracts;

public sealed record InclusionProofReference(
    string BatchId,
    string MerkleRoot,
    string LeafCommitment,
    int LeafIndex,
    string? ProofUri,
    string? ProofHash);

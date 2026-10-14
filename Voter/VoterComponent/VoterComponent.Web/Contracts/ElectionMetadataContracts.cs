namespace VoterComponent.Web.Contracts;

public sealed record ElectionMetadataDocument(
    ElectionMetadataElection Election,
    ElectionMetadataVoting Voting,
    ElectionMetadataBlockchain Blockchain,
    IReadOnlyList<ElectionMetadataCandidate> Candidates,
    ElectionMetadataVerification Verification);

public sealed record ElectionMetadataElection(
    string Id,
    string Name,
    string Description,
    string Version);

public sealed record ElectionMetadataVoting(
    long OpeningTimestamp,
    long ClosingTimestamp);

public sealed record ElectionMetadataBlockchain(
    string Network,
    string ProgramId,
    string ElectionPda);

public sealed record ElectionMetadataCandidate(
    int Id,
    string Name,
    string Party,
    string Description,
    string Image);

public sealed record ElectionMetadataVerification(
    string MetadataHash,
    long CreationTimestamp);
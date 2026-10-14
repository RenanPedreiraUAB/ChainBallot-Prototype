using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

public interface IElectionMetadataClient
{
    Task<ElectionMetadataDocument?> GetElectionMetadataAsync(string electionId, CancellationToken cancellationToken);
}
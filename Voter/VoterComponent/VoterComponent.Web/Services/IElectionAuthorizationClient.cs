using ChainBallot.PublicContracts;

namespace VoterComponent.Web.Services;

public interface IElectionAuthorizationClient
{
    Task<VoterAuthorizationProfile?> GetProfileAsync(CancellationToken cancellationToken);

    Task<ElectionAuthorizationState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken);

    Task<AuthorizationGrant?> IssueGrantAsync(string electionId, CancellationToken cancellationToken);

    Task<ConsumeAuthorizationResponse?> ConsumeGrantAsync(string electionId, Guid grantId, string submissionNonce, CancellationToken cancellationToken);
}
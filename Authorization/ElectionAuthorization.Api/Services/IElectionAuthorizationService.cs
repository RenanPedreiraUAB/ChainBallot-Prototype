using ChainBallot.PublicContracts;

namespace ElectionAuthorization.Api.Services;

/// <summary>
/// Defines election authorization operations for VID profile, grant issuance, and grant consumption.
/// </summary>
public interface IElectionAuthorizationService
{
    /// <summary>
    /// Gets or creates a VID profile for an authenticated subject.
    /// </summary>
    /// <param name="subject">Trusted authenticator subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>VID profile.</returns>
    Task<VoterAuthorizationProfile> GetOrCreateProfileAsync(string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Gets election authorization state for a subject.
    /// </summary>
    /// <param name="subject">Trusted authenticator subject identifier.</param>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Election authorization state.</returns>
    Task<ElectionAuthorizationState> GetElectionStateAsync(string subject, string electionId, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a one-time authorization grant for vote submission.
    /// </summary>
    /// <param name="subject">Trusted authenticator subject identifier.</param>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Issued authorization grant when eligible; otherwise null.</returns>
    Task<AuthorizationGrant?> IssueGrantAsync(string subject, string electionId, CancellationToken cancellationToken);

    /// <summary>
    /// Consumes an authorization grant and vote eligibility state.
    /// </summary>
    /// <remarks>
    /// Election-, VID- and nonce-bound; atomic and idempotent for a repeated call with the same grant
    /// and nonce. Callers (e.g. the Voter Component) must only invoke this after the corresponding ballot
    /// package has already been durably staged, so no voting right is ever consumed without a recoverable record of the submission it authorizes.
    /// </remarks>
    /// <param name="subject">Trusted authenticator subject identifier.</param>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="request">Consume request payload.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Grant consume result.</returns>
    Task<ConsumeAuthorizationResponse> ConsumeGrantAsync(string subject, string electionId, ConsumeAuthorizationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sets election eligibility for a trusted subject.
    /// </summary>
    /// <param name="subject">Trusted authenticator subject identifier.</param>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="isEligible">Eligibility value to set.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Updated election authorization state.</returns>
    Task<ElectionAuthorizationState> SetEligibilityAsync(string subject, string electionId, bool isEligible, CancellationToken cancellationToken);
}

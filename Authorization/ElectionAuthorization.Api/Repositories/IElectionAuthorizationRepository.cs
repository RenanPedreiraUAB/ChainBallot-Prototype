using ElectionAuthorization.Api.Data;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Defines persistence operations for election-specific authorization state.
/// </summary>
public interface IElectionAuthorizationRepository
{
    /// <summary>
    /// Gets election authorization state for a VID.
    /// </summary>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="vid">VID identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>State record when found; otherwise null.</returns>
    Task<ElectionVoterAuthorization?> GetAsync(string electionId, string vid, CancellationToken cancellationToken);

    /// <summary>
    /// Creates election authorization state for a VID.
    /// </summary>
    /// <param name="state">State entity to store.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task AddAsync(ElectionVoterAuthorization state, CancellationToken cancellationToken);

    /// <summary>
    /// Upserts election eligibility for a VID.
    /// </summary>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="vid">VID identifier.</param>
    /// <param name="isEligible">Eligibility value to set.</param>
    /// <param name="updatedAtUtc">Mutation timestamp in UTC.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Updated authorization state.</returns>
    Task<ElectionVoterAuthorization> UpsertEligibilityAsync(string electionId, string vid, bool isEligible, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to consume vote eligibility atomically.
    /// </summary>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="vid">VID identifier.</param>
    /// <param name="consumedAtUtc">Consumption timestamp in UTC.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>True when vote consumption succeeds; otherwise false.</returns>
    Task<bool> TryConsumeVoteAsync(string electionId, string vid, DateTimeOffset consumedAtUtc, CancellationToken cancellationToken);
}

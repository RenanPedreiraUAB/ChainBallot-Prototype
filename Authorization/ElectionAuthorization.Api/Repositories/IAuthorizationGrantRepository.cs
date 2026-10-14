using ElectionAuthorization.Api.Data;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Defines persistence operations for one-time vote authorization grants.
/// </summary>
public interface IAuthorizationGrantRepository
{
    /// <summary>
    /// Persists an issued authorization grant.
    /// </summary>
    /// <param name="grant">Grant entity to store.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task AddAsync(AuthorizationGrantRecord grant, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a grant by identifier.
    /// </summary>
    /// <param name="grantId">Grant identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Grant record when found; otherwise null.</returns>
    Task<AuthorizationGrantRecord?> GetAsync(Guid grantId, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to mark a grant as consumed.
    /// </summary>
    /// <param name="grantId">Grant identifier.</param>
    /// <param name="submissionNonce">Client nonce used for traceability.</param>
    /// <param name="consumedAtUtc">Consumption timestamp in UTC.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>True when consume transition succeeds; otherwise false.</returns>
    Task<bool> TryConsumeAsync(Guid grantId, string submissionNonce, DateTimeOffset consumedAtUtc, CancellationToken cancellationToken);
}

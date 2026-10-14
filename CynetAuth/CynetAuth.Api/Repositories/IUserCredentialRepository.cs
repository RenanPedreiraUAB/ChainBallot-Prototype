using CynetAuth.Api.Data;

namespace CynetAuth.Api.Repositories;

/// <summary>
/// Defines persistence operations for local credential-based user accounts.
/// </summary>
public interface IUserCredentialRepository
{
    /// <summary>
    /// Checks whether a username already exists using normalized comparison.
    /// </summary>
    /// <param name="normalizedUsername">Normalized username value.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>True when a matching username exists.</returns>
    Task<bool> ExistsByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the active account matching a normalized username.
    /// </summary>
    /// <param name="normalizedUsername">Normalized username value.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Matching active user when found; otherwise null.</returns>
    Task<UserCredential?> GetActiveByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new user account.
    /// </summary>
    /// <param name="user">User account entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task AddAsync(UserCredential user, CancellationToken cancellationToken);
}

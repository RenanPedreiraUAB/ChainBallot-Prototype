using ElectionAuthorization.Api.Data;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Defines persistence operations for subject to VID mapping.
/// </summary>
public interface IVoterIdentityRepository
{
    /// <summary>
    /// Gets an identity mapping by trusted subject identifier.
    /// </summary>
    /// <param name="subject">Trusted subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Identity mapping when found; otherwise null.</returns>
    Task<VoterIdentityMapping?> GetBySubjectAsync(string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new identity mapping.
    /// </summary>
    /// <param name="mapping">Identity mapping to store.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task AddAsync(VoterIdentityMapping mapping, CancellationToken cancellationToken);
}

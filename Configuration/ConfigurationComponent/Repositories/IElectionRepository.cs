using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Repositories;

/// <summary>
/// Defines persistence operations for election configurations.
/// </summary>
public interface IElectionRepository
{
    /// <summary>
    /// Gets all stored elections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Collection of stored elections.</returns>
    Task<IReadOnlyList<ElectionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single election by its identifier.
    /// </summary>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The election if found; otherwise <see langword="null"/>.</returns>
    Task<ElectionConfiguration?> GetByIdAsync(Guid electionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new or existing election.
    /// </summary>
    /// <param name="election">Election state to persist.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task SaveAsync(ElectionConfiguration election, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an election by identifier.
    /// </summary>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task DeleteAsync(Guid electionId, CancellationToken cancellationToken = default);
}

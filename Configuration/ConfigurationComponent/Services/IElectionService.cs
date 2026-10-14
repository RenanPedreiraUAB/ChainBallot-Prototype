using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines draft election lifecycle operations for the configuration component.
/// </summary>
public interface IElectionService
{
    /// <summary>
    /// Gets all elections known to the configuration component.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Collection of stored elections.</returns>
    Task<IReadOnlyList<ElectionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and persists a new election in draft state using default values.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Created draft election.</returns>
    Task<ElectionConfiguration> CreateDraftAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves updates to an existing draft election.
    /// </summary>
    /// <param name="election">Draft election state to persist.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Updated persisted election.</returns>
    Task<ElectionConfiguration> SaveDraftAsync(ElectionConfiguration election, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an election while it remains in draft state.
    /// </summary>
    /// <param name="electionId">Election identifier.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task DeleteDraftAsync(Guid electionId, CancellationToken cancellationToken = default);
}

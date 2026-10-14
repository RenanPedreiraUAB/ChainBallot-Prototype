namespace ConfigurationComponent.Repositories;

/// <summary>
/// Defines persistence operations for deployment manifest artifacts.
/// </summary>
public interface IDeploymentManifestRepository
{
    /// <summary>
    /// Saves a deployment manifest for an election.
    /// </summary>
    /// <param name="electionId">Election identifier used to name or partition the artifact.</param>
    /// <param name="manifestJson">Serialized deployment manifest payload.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Path or URI where the manifest was stored.</returns>
    Task<string> SaveAsync(string electionId, string manifestJson, CancellationToken cancellationToken = default);
}

namespace ConfigurationComponent.Infrastructure;

/// <summary>
/// Defines how election metadata is published to an external metadata store.
/// </summary>
public interface IMetadataRepository
{
    /// <summary>
    /// Uploads serialized metadata and returns the persistent metadata URI.
    /// </summary>
    /// <param name="endpoint">Base endpoint of the metadata service.</param>
    /// <param name="metadataJson">Serialized metadata JSON payload.</param>
    /// <param name="electionId">Election identifier associated with the payload.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Resolvable URI that identifies the uploaded metadata.</returns>
    Task<string> UploadMetadataAsync(
        string endpoint,
        string metadataJson,
        string electionId,
        CancellationToken cancellationToken = default);
}

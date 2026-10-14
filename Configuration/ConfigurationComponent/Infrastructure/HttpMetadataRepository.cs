using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ConfigurationComponent.Infrastructure;

/// <summary>
/// Represents Http Metadata Repository.
/// </summary>
public sealed class HttpMetadataRepository(ILogger<HttpMetadataRepository> logger) : IMetadataRepository
{
    private readonly ILogger<HttpMetadataRepository> _logger = logger;

    /// <summary>
    /// Executes Upload Metadata Async.
    /// </summary>
    public async Task<string> UploadMetadataAsync(
        string endpoint,
        string metadataJson,
        string electionId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            electionId,
            metadata = metadataJson,
            createdAt = DateTimeOffset.UtcNow
        };

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid metadata endpoint: {endpoint}");
        }

        using var httpClient = new HttpClient { BaseAddress = baseUri };
        using var response = await httpClient.PostAsJsonAsync("metadata", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location?.ToString();
        if (!string.IsNullOrWhiteSpace(location))
        {
            return location;
        }

        _logger.LogInformation("Metadata uploaded for election {ElectionId} to {Endpoint}", electionId, endpoint);

        return $"metadata://{electionId}";
    }
}

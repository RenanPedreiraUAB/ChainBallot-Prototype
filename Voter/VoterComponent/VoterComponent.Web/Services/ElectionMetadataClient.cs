using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// HTTP client boundary from the Voter Component to the Election Metadata Repository
/// (Architecture.md § 2.1), used to render the ballot UI with the published candidate list/schedule.
/// </summary>
public sealed class ElectionMetadataClient(HttpClient httpClient, IOptions<VoterOptions> options) : IElectionMetadataClient
{
    public async Task<ElectionMetadataDocument?> GetElectionMetadataAsync(string electionId, CancellationToken cancellationToken)
    {
        var template = options.Value.MetadataEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var requestUri = template.Replace("{electionId}", Uri.EscapeDataString(electionId), StringComparison.Ordinal);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ElectionMetadataDocument>(cancellationToken: cancellationToken);
    }
}
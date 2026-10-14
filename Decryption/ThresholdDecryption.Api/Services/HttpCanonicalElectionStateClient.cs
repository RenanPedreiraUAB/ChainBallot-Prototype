using System.Net.Http.Json;
using System.Text.Json;

namespace ThresholdDecryption.Api.Services;

/// <summary>
/// HTTP-backed <see cref="ICanonicalElectionStateClient"/>. Fails closed: an unreachable endpoint,
/// non-success response, or malformed body all return null, which the caller must treat as "not
/// confirmed Closed/Finalized".
/// </summary>
public sealed class HttpCanonicalElectionStateClient(HttpClient httpClient) : ICanonicalElectionStateClient
{
    public async Task<CanonicalElectionStateDto?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return null;
        }

        try
        {
            using var response = await httpClient
                .GetAsync($"api/transparency/elections/{Uri.EscapeDataString(electionId)}/canonical-state", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<CanonicalElectionStateDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }
}

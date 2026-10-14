using System.Net;
using System.Net.Http.Json;
using ChainBallot.PublicContracts;
using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

public sealed class HttpTransparencyAuditReadClient : ITransparencyAuditReadClient
{
    private readonly HttpClient _httpClient;

    public HttpTransparencyAuditReadClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<UpstreamReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken)
        => GetAsync<VerifiedResultSnapshot>($"api/transparency/elections/{Uri.EscapeDataString(electionId)}/snapshot", cancellationToken);

    public Task<UpstreamReadResult<ProjectionStatusDto>> GetProjectionStatusAsync(string electionId, CancellationToken cancellationToken)
        => GetAsync<ProjectionStatusDto>($"api/transparency/elections/{Uri.EscapeDataString(electionId)}/projection-status", cancellationToken);

    public Task<UpstreamReadResult<CanonicalElectionStateDto>> GetCanonicalStateAsync(string electionId, CancellationToken cancellationToken)
        => GetAsync<CanonicalElectionStateDto>($"api/transparency/elections/{Uri.EscapeDataString(electionId)}/canonical-state", cancellationToken);

    private async Task<UpstreamReadResult<T>> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(relativePath, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpstreamReadResult<T>(UpstreamReadStatus.NotFound, default, "Resource not found.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new UpstreamReadResult<T>(UpstreamReadStatus.Unauthorized, default, "Transparency read unauthorized.");
            }

            if ((int)response.StatusCode >= 500)
            {
                return new UpstreamReadResult<T>(UpstreamReadStatus.Unavailable, default, "Transparency service unavailable.");
            }

            response.EnsureSuccessStatusCode();
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return value is null
                ? new UpstreamReadResult<T>(UpstreamReadStatus.Error, default, "Empty response payload.")
                : new UpstreamReadResult<T>(UpstreamReadStatus.Success, value, null);
        }
        catch (TaskCanceledException)
        {
            return new UpstreamReadResult<T>(UpstreamReadStatus.Unavailable, default, "Request timed out.");
        }
        catch (HttpRequestException)
        {
            return new UpstreamReadResult<T>(UpstreamReadStatus.Unavailable, default, "Network failure.");
        }
    }
}

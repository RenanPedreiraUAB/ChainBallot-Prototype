using System.Net.Http.Json;
using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

public sealed class HttpAuthoritativeDigestSource : IAuthoritativeDigestSource
{
    private readonly HttpClient _httpClient;
    private readonly string _metadataTemplate;
    private readonly string _auditTemplate;

    public HttpAuthoritativeDigestSource(HttpClient httpClient, string metadataTemplate, string auditTemplate)
    {
        _httpClient = httpClient;
        _metadataTemplate = metadataTemplate;
        _auditTemplate = auditTemplate;
    }

    public async Task<AuthoritativeDigestPair> GetDigestsAsync(string electionId, CancellationToken cancellationToken)
    {
        var metadataDigest = await GetDigestAsync(_metadataTemplate, electionId, cancellationToken).ConfigureAwait(false);
        var auditDigest = await GetDigestAsync(_auditTemplate, electionId, cancellationToken).ConfigureAwait(false);
        return new AuthoritativeDigestPair(metadataDigest, auditDigest);
    }

    private async Task<string?> GetDigestAsync(string template, string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var endpoint = template.Replace("{electionId}", Uri.EscapeDataString(electionId), StringComparison.OrdinalIgnoreCase);
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<DigestResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return payload?.Digest;
        }
        catch
        {
            return null;
        }
    }

    private sealed record DigestResponse(string Digest);
}

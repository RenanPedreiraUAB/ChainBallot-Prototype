using System.Text.Json;

namespace TransparencyAudit.Api.Services;

public sealed class HttpAuthoritativeEvidenceDigestSource : IAuthoritativeEvidenceDigestSource
{
    private readonly HttpClient _httpClient;
    private readonly string? _metadataTemplate;
    private readonly string? _auditTemplate;

    public HttpAuthoritativeEvidenceDigestSource(HttpClient httpClient, string? metadataTemplate, string? auditTemplate)
    {
        _httpClient = httpClient;
        _metadataTemplate = metadataTemplate;
        _auditTemplate = auditTemplate;
    }

    public Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken)
        => GetDigestAsync(_metadataTemplate, electionId, cancellationToken);

    public Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken)
        => GetDigestAsync(_auditTemplate, electionId, cancellationToken);

    private async Task<string?> GetDigestAsync(string? endpointTemplate, string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpointTemplate) || string.IsNullOrWhiteSpace(electionId))
        {
            return null;
        }

        var encodedId = Uri.EscapeDataString(electionId);
        var url = endpointTemplate.Replace("{electionId}", encodedId, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            body = body.Trim();

            if (body.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("digest", out var digestElement) && digestElement.ValueKind == JsonValueKind.String)
                    {
                        return digestElement.GetString();
                    }

                    if (doc.RootElement.TryGetProperty("resultDigest", out var resultDigestElement) && resultDigestElement.ValueKind == JsonValueKind.String)
                    {
                        return resultDigestElement.GetString();
                    }
                }
                catch (JsonException)
                {
                    return null;
                }

                return null;
            }

            return body.Trim('"');
        }
    }
}

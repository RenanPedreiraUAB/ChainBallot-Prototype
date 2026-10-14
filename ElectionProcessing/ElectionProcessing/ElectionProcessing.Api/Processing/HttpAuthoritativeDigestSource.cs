using System.Text.Json;

namespace ElectionProcessing.Api.Processing;

/// <summary>Fetches authoritative digests via HTTP, mirroring Transparency's own HttpAuthoritativeEvidenceDigestSource so both sides read the same value. Best-effort: any failure (unreachable, timeout, malformed body) yields null rather than throwing, since a missing digest is a correlation concern for Transparency, not a processing failure here.</summary>
public sealed class HttpAuthoritativeDigestSource(HttpClient httpClient, string? metadataTemplate, string? auditTemplate) : IAuthoritativeDigestSource
{
    public Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken)
        => GetDigestAsync(metadataTemplate, electionId, cancellationToken);

    public Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken)
        => GetDigestAsync(auditTemplate, electionId, cancellationToken);

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
            response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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

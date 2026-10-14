using System.Text.Json;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// HTTP-backed <see cref="ISolanaSettlementReadAdapter"/> implementation for reading canonical
/// settlement-ledger state (Architecture.md § 3.7/§ 3.8). Alternative adapter modes
/// (<c>ProjectionOnly</c>, <c>Mock</c>) exist so the read layer can be exercised without a live ledger.
/// </summary>
public sealed class HttpSolanaSettlementReadAdapter : ISolanaSettlementReadAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string? _endpointTemplate;

    public HttpSolanaSettlementReadAdapter(HttpClient httpClient, string? endpointTemplate)
    {
        _httpClient = httpClient;
        _endpointTemplate = endpointTemplate;
    }

    public async Task<CanonicalElectionState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId) || string.IsNullOrWhiteSpace(_endpointTemplate))
        {
            return null;
        }

        if (!_endpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase))
        {
            return await GetRpcStateAsync(electionId, cancellationToken).ConfigureAwait(false);
        }

        var url = _endpointTemplate.Replace("{electionId}", Uri.EscapeDataString(electionId), StringComparison.OrdinalIgnoreCase);
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

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var finalized = root.TryGetProperty("isFinalized", out var finalizedNode) && finalizedNode.ValueKind == JsonValueKind.True
                    || (finalizedNode.ValueKind == JsonValueKind.False ? false : ParseBool(root, "finalized"));

                var note = root.TryGetProperty("note", out var noteNode) && noteNode.ValueKind == JsonValueKind.String
                    ? noteNode.GetString()
                    : null;

                return new CanonicalElectionState(
                    electionId,
                    finalized,
                    "HttpSolana",
                    DateTimeOffset.UtcNow,
                    note);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private async Task<CanonicalElectionState?> GetRpcStateAsync(string electionId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _endpointTemplate,
                new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "getHealth",
                    @params = Array.Empty<object>()
                },
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out _))
            {
                return null;
            }

            return new CanonicalElectionState(
                electionId,
                true,
                "HttpSolanaRpc",
                DateTimeOffset.UtcNow,
                "Resolved from Solana RPC health endpoint.");
        }
        catch
        {
            return null;
        }
    }

    private static bool ParseBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node))
        {
            return false;
        }

        return node.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(node.GetString(), out var parsed) && parsed,
            _ => false
        };
    }
}

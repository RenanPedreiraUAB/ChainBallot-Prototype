using System.Net;
using System.Net.Http.Json;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Http;

namespace ResultPortal.Web.Services;

/// <summary>
/// The Result Portal's sole data-access path (Architecture.md § 3.9): calls the Transparency &amp;
/// Audit Component's HTTP API for snapshots and VID verification. The portal holds no independent
/// repository of its own — every value it renders is sourced through this client.
/// </summary>
public sealed class TransparencyReadClient : ITransparencyReadClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TransparencyReadClient> _logger;

    public TransparencyReadClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<TransparencyReadClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<TransparencyReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return TransparencyReadResult<VerifiedResultSnapshot>.Error("none", "Election id is required.");
        }

        var correlationId = GetCorrelationId();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/transparency/elections/{Uri.EscapeDataString(electionId)}/snapshot");
        request.Headers.Add("X-Correlation-Id", correlationId);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return TransparencyReadResult<VerifiedResultSnapshot>.NotFound(correlationId, "No verified snapshot exists for the requested election.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return TransparencyReadResult<VerifiedResultSnapshot>.Unauthorized(correlationId, "Transparency API denied this read operation.");
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogWarning("Transparency snapshot request failed with upstream status {StatusCode}. ElectionId={ElectionId} CorrelationId={CorrelationId}",
                    (int)response.StatusCode,
                    electionId,
                    correlationId);
                return TransparencyReadResult<VerifiedResultSnapshot>.Unavailable(correlationId, "Transparency API is unavailable.");
            }

            response.EnsureSuccessStatusCode();
            var snapshot = await response.Content.ReadFromJsonAsync<VerifiedResultSnapshot>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return TransparencyReadResult<VerifiedResultSnapshot>.Error(correlationId, "Transparency API returned an empty snapshot response.");
            }

            return TransparencyReadResult<VerifiedResultSnapshot>.Success(snapshot, correlationId);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Transparency snapshot request timed out. ElectionId={ElectionId} CorrelationId={CorrelationId}", electionId, correlationId);
            return TransparencyReadResult<VerifiedResultSnapshot>.Unavailable(correlationId, "Transparency API request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Transparency snapshot request failed due to network error. ElectionId={ElectionId} CorrelationId={CorrelationId}", electionId, correlationId);
            return TransparencyReadResult<VerifiedResultSnapshot>.Unavailable(correlationId, "Transparency API is not reachable.");
        }
    }

    public async Task<TransparencyReadResult<VidVerificationResponse>> VerifyByVidAsync(string electionId, string vid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId) || string.IsNullOrWhiteSpace(vid))
        {
            return TransparencyReadResult<VidVerificationResponse>.Error("none", "Election id and VID are required.");
        }

        var correlationId = GetCorrelationId();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/transparency/elections/{Uri.EscapeDataString(electionId)}/verify/{Uri.EscapeDataString(vid)}");
        request.Headers.Add("X-Correlation-Id", correlationId);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return TransparencyReadResult<VidVerificationResponse>.NotFound(correlationId, "Verification record was not found for the provided election id and VID.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return TransparencyReadResult<VidVerificationResponse>.Unauthorized(correlationId, "Transparency API denied this verification read operation.");
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogWarning("Transparency verify request failed with upstream status {StatusCode}. ElectionId={ElectionId} Vid={Vid} CorrelationId={CorrelationId}",
                    (int)response.StatusCode,
                    electionId,
                    vid,
                    correlationId);
                return TransparencyReadResult<VidVerificationResponse>.Unavailable(correlationId, "Transparency API is unavailable.");
            }

            response.EnsureSuccessStatusCode();
            var verification = await response.Content.ReadFromJsonAsync<VidVerificationResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (verification is null)
            {
                return TransparencyReadResult<VidVerificationResponse>.Error(correlationId, "Transparency API returned an empty verification response.");
            }

            return TransparencyReadResult<VidVerificationResponse>.Success(verification, correlationId);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Transparency verify request timed out. ElectionId={ElectionId} Vid={Vid} CorrelationId={CorrelationId}", electionId, vid, correlationId);
            return TransparencyReadResult<VidVerificationResponse>.Unavailable(correlationId, "Transparency API request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Transparency verify request failed due to network error. ElectionId={ElectionId} Vid={Vid} CorrelationId={CorrelationId}", electionId, vid, correlationId);
            return TransparencyReadResult<VidVerificationResponse>.Unavailable(correlationId, "Transparency API is not reachable.");
        }
    }

    private string GetCorrelationId()
    {
        var traceIdentifier = _httpContextAccessor.HttpContext?.TraceIdentifier;
        return string.IsNullOrWhiteSpace(traceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : traceIdentifier;
    }
}

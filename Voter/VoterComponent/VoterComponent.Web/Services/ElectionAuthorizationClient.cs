using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using ChainBallot.PublicContracts;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// HTTP client boundary from the Voter Component to the Election Authorization Component
/// (Architecture.md § 3.3): VID profile lookup, election eligibility state, and one-time authorization
/// grant issue/consume, all forwarding the voter's bearer token.
/// </summary>
public sealed class ElectionAuthorizationClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor) : IElectionAuthorizationClient
{
    public async Task<VoterAuthorizationProfile?> GetProfileAsync(CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/authorization/profile", cancellationToken);
        return await DeserializeOrNullAsync<VoterAuthorizationProfile>(response, cancellationToken);
    }

    public async Task<ElectionAuthorizationState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, $"/api/authorization/elections/{Uri.EscapeDataString(electionId)}/state", cancellationToken);
        return await DeserializeOrNullAsync<ElectionAuthorizationState>(response, cancellationToken);
    }

    public async Task<AuthorizationGrant?> IssueGrantAsync(string electionId, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, $"/api/authorization/elections/{Uri.EscapeDataString(electionId)}/grant", cancellationToken);
        return await DeserializeOrNullAsync<AuthorizationGrant>(response, cancellationToken);
    }

    public async Task<ConsumeAuthorizationResponse?> ConsumeGrantAsync(string electionId, Guid grantId, string submissionNonce, CancellationToken cancellationToken)
    {
        var request = new ConsumeAuthorizationRequest(grantId, submissionNonce);
        var response = await SendAuthorizedAsync(HttpMethod.Post, $"/api/authorization/elections/{Uri.EscapeDataString(electionId)}/consume", request, cancellationToken);
        return await DeserializeOrNullAsync<ConsumeAuthorizationResponse>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string requestUri, CancellationToken cancellationToken)
    {
        return await SendAuthorizedAsync(method, requestUri, content: null, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string requestUri, object? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        AttachBearerToken(request);

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private void AttachBearerToken(HttpRequestMessage request)
    {
        var token = httpContextAccessor.HttpContext?.User.FindFirstValue(SessionClaimNames.AccessToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static async Task<T?> DeserializeOrNullAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
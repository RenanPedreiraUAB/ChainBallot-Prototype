using System.Net.Http.Json;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Default <see cref="IAuthProvider"/> implementation targeting CynetAuth, this prototype's instance of
/// the Trusted Third Party (TTP) Authentication Service role (Architecture.md § 3.2 / Prototype.md § 4.2).
/// Kept behind the <see cref="IAuthProvider"/> abstraction so the TTP instance can be replaced without
/// changing the rest of the Voter Component.
/// </summary>
public sealed class CynetAuthAuthProvider(HttpClient httpClient) : IAuthProvider
{
    public async Task<AuthResponse?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
    }
}
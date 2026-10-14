using System.Security.Claims;
using System.Text.Encodings.Web;
using ElectionProcessing.Api.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Security;

/// <summary>
/// Header-token authentication for Processing's privileged operational routes (Architecture.md § 3.6).
/// The caller presents a configured token via <see cref="ProcessingAuthenticationOptions.HeaderName"/>;
/// the matching credential's configured roles become the caller's role claims, so per-route
/// authorization (<c>RequireRole</c>) can distinguish operator vs. ingest-service callers.
/// </summary>
public sealed class ProcessingHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<ProcessingOptions> processingOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    public const string SchemeName = "ProcessingHeader";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settings = processingOptions.Value.Authentication;

        if (!Request.Headers.TryGetValue(settings.HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var credential = settings.Credentials.Find(c => string.Equals(c.Token, provided.ToString(), StringComparison.Ordinal));
        if (credential is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid processing operator/service token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(credential.Name) ? "processing-service" : credential.Name)
        };
        claims.AddRange(credential.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

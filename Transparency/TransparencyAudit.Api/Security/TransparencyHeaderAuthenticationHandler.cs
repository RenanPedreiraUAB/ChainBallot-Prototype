using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TransparencyAudit.Api.Application;

namespace TransparencyAudit.Api.Security;

/// <summary>
/// Header-token authentication for Transparency's privileged routes (Architecture.md § 3.8). The caller
/// presents a configured token via <see cref="TransparencyAuthenticationOptions.HeaderName"/>; the
/// matching credential's configured roles become the caller's role claims, and its name becomes the
/// attributable actor identity for reconciliation audit events.
/// </summary>
public sealed class TransparencyHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<TransparencyOptions> transparencyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    public const string SchemeName = "TransparencyHeader";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settings = transparencyOptions.Value.Authentication;

        if (!Request.Headers.TryGetValue(settings.HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var credential = settings.Credentials.Find(c => string.Equals(c.Token, provided.ToString(), StringComparison.Ordinal));
        if (credential is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid transparency operator/service token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(credential.Name) ? "transparency-service" : credential.Name)
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

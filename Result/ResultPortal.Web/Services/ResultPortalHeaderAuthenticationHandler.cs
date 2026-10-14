using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ResultPortal.Web.Application;

namespace ResultPortal.Web.Services;

public sealed class ResultPortalHeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ResultPortalHeader";

    private readonly IOptions<ResultPortalOptions> _portalOptions;

    public ResultPortalHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ResultPortalOptions> portalOptions)
        : base(options, logger, encoder)
    {
        _portalOptions = portalOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var options = _portalOptions.Value;
        if (!string.Equals(options.AuthMode, "HeaderToken", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerName = string.IsNullOrWhiteSpace(options.HeaderAuthHeaderName)
            ? "X-ResultPortal-Auth"
            : options.HeaderAuthHeaderName;

        if (!Request.Headers.TryGetValue(headerName, out var providedToken))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!string.Equals(providedToken.ToString(), options.HeaderAuthToken, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authentication token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "result-portal-header-user"),
            new(ClaimTypes.Name, "Result Portal Header User")
        };

        foreach (var role in options.HeaderAuthRoles ?? [])
        {
            var trimmedRole = role?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedRole))
            {
                claims.Add(new Claim(ClaimTypes.Role, trimmedRole));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

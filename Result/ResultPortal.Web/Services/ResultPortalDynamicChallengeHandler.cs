using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ResultPortal.Web.Application;

namespace ResultPortal.Web.Services;

public sealed class ResultPortalDynamicChallengeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ResultPortalDynamicChallenge";

    private readonly IOptions<ResultPortalOptions> _portalOptions;

    public ResultPortalDynamicChallengeHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ResultPortalOptions> portalOptions)
        : base(options, logger, encoder)
    {
        _portalOptions = portalOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var mode = _portalOptions.Value.AuthMode;
        if (string.Equals(mode, "Oidc", StringComparison.OrdinalIgnoreCase))
        {
            var authority = (_portalOptions.Value.OidcAuthority ?? string.Empty).Trim();
            if (authority.Length == 0)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            var callback = (_portalOptions.Value.OidcCallbackPath ?? "/signin-oidc").Trim();
            var normalizedCallback = callback.StartsWith("/", StringComparison.Ordinal) ? callback : "/" + callback;
            var redirectUri = $"{Request.Scheme}://{Request.Host}{normalizedCallback}";

            var challengeUri = authority.TrimEnd('/') +
                               "/authorize?client_id=" + Uri.EscapeDataString(_portalOptions.Value.OidcClientId ?? string.Empty) +
                               "&response_type=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(_portalOptions.Value.OidcResponseType) ? "code" : _portalOptions.Value.OidcResponseType) +
                               "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                               "&scope=" + Uri.EscapeDataString(string.Join(' ', _portalOptions.Value.OidcScopes ?? ["openid", "profile"]));

            Response.Redirect(challengeUri);
            return Task.CompletedTask;
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}

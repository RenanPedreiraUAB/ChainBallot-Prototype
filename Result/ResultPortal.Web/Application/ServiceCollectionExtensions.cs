using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using ResultPortal.Web.Services;

namespace ResultPortal.Web.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResultPortal(this IServiceCollection services, IConfiguration configuration)
    {
        var optionsSection = configuration.GetSection(ResultPortalOptions.SectionName);
        services.AddOptions<ResultPortalOptions>()
            .Bind(optionsSection)
            .Validate(x => x.TransparencyTimeoutSeconds > 0, "ResultPortal:TransparencyTimeoutSeconds must be greater than zero.")
            .Validate(x =>
                Uri.TryCreate(x.TransparencyBaseUrl, UriKind.Absolute, out _),
                "ResultPortal:TransparencyBaseUrl must be an absolute URI.")
            .Validate(x =>
                string.Equals(x.AuthMode, "Open", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.AuthMode, "HeaderToken", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.AuthMode, "Oidc", StringComparison.OrdinalIgnoreCase),
                "ResultPortal:AuthMode must be Open, HeaderToken, or Oidc.")
            .Validate(x =>
                !string.Equals(x.AuthMode, "HeaderToken", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.HeaderAuthHeaderName) && !string.IsNullOrWhiteSpace(x.HeaderAuthToken)),
                "ResultPortal header auth requires HeaderAuthHeaderName and HeaderAuthToken.")
            .Validate(x =>
                !string.Equals(x.AuthMode, "Oidc", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.OidcAuthority)
                    && !string.IsNullOrWhiteSpace(x.OidcClientId)
                    && Uri.TryCreate(x.OidcAuthority, UriKind.Absolute, out _)
                    && (x.OidcScopes?.Length ?? 0) > 0),
                "ResultPortal OIDC mode requires valid OidcAuthority, OidcClientId, and at least one scope.")
            .ValidateOnStart();
        services.AddRazorPages();
        services.AddHttpContextAccessor();
        services.AddSingleton<IElectionAccessPolicy, ConfiguredElectionAccessPolicy>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = ResultPortalHeaderAuthenticationHandler.SchemeName;
                options.DefaultAuthenticateScheme = ResultPortalHeaderAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = ResultPortalDynamicChallengeHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, ResultPortalHeaderAuthenticationHandler>(
                ResultPortalHeaderAuthenticationHandler.SchemeName,
                _ =>
                {
                })
            .AddScheme<AuthenticationSchemeOptions, ResultPortalNoAuthHandler>(
                ResultPortalNoAuthHandler.SchemeName,
                _ =>
                {
                })
            .AddScheme<AuthenticationSchemeOptions, ResultPortalDynamicChallengeHandler>(
                ResultPortalDynamicChallengeHandler.SchemeName,
                _ =>
                {
                })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                var snapshot = optionsSection.Get<ResultPortalOptions>() ?? new ResultPortalOptions();

                options.Authority = string.IsNullOrWhiteSpace(snapshot.OidcAuthority)
                    ? "https://placeholder.identity.local"
                    : snapshot.OidcAuthority;
                options.ClientId = string.IsNullOrWhiteSpace(snapshot.OidcClientId)
                    ? "result-portal"
                    : snapshot.OidcClientId;
                options.ClientSecret = snapshot.OidcClientSecret;
                options.ResponseType = string.IsNullOrWhiteSpace(snapshot.OidcResponseType) ? "code" : snapshot.OidcResponseType;
                options.CallbackPath = string.IsNullOrWhiteSpace(snapshot.OidcCallbackPath) ? "/signin-oidc" : snapshot.OidcCallbackPath;
                options.SignedOutCallbackPath = string.IsNullOrWhiteSpace(snapshot.OidcSignedOutCallbackPath)
                    ? "/signout-callback-oidc"
                    : snapshot.OidcSignedOutCallbackPath;
                options.RequireHttpsMetadata = snapshot.OidcRequireHttpsMetadata;
                options.UsePkce = snapshot.OidcUsePkce;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.Scope.Clear();
                foreach (var scope in snapshot.OidcScopes ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(scope))
                    {
                        options.Scope.Add(scope.Trim());
                    }
                }
            });

        services.AddAuthorization();

        services.AddHttpClient<ITransparencyReadClient, TransparencyReadClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ResultPortalOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.TransparencyBaseUrl)
                ? "http://localhost:5284"
                : options.TransparencyBaseUrl;

            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TransparencyTimeoutSeconds));
        });

        return services;
    }
}

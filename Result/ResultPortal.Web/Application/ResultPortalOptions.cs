namespace ResultPortal.Web.Application;

public sealed class ResultPortalOptions
{
    public const string SectionName = "ResultPortal";

    public string TransparencyBaseUrl { get; set; } = "http://localhost:5284";

    public int TransparencyTimeoutSeconds { get; set; } = 10;

    public string AuthMode { get; set; } = "Open";

    public string HeaderAuthHeaderName { get; set; } = "X-ResultPortal-Auth";

    public string HeaderAuthToken { get; set; } = "change-me-in-production";

    public string[] HeaderAuthRoles { get; set; } = ["result-reader"];

    public string OidcAuthority { get; set; } = string.Empty;

    public string OidcClientId { get; set; } = string.Empty;

    public string OidcClientSecret { get; set; } = string.Empty;

    public string OidcResponseType { get; set; } = "code";

    public string OidcCallbackPath { get; set; } = "/signin-oidc";

    public string OidcSignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    public bool OidcUsePkce { get; set; } = true;

    public bool OidcRequireHttpsMetadata { get; set; } = true;

    public string[] OidcScopes { get; set; } = ["openid", "profile", "email"];

    public string DefaultElectionVisibility { get; set; } = "Public";

    public string[] PublicElectionIds { get; set; } = [];

    public string[] PrivateElectionIds { get; set; } = [];

    public string PrivateElectionViewerRole { get; set; } = "result-reader";

    public bool HidePrivateElectionExistence { get; set; } = true;
}

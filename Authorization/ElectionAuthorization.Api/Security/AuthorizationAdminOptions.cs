namespace ElectionAuthorization.Api.Security;

/// <summary>
/// Admin-only options for local utility operations.
/// </summary>
public sealed class AuthorizationAdminOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Admin";

    /// <summary>
    /// Gets or sets seed key required by admin-only eligibility endpoint.
    /// </summary>
    public string SeedKey { get; set; } = string.Empty;
}

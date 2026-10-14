namespace ElectionAuthorization.Api.Security;

/// <summary>
/// JWT validation options used to trust the external TTP authenticator.
/// </summary>
public sealed class AuthorizationJwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TrustedAuth";

    /// <summary>
    /// Gets or sets token issuer expected from trusted authenticator.
    /// </summary>
    public string Issuer { get; set; } = "cynetauth";

    /// <summary>
    /// Gets or sets token audience expected by this component.
    /// </summary>
    public string Audience { get; set; } = "chainballot-authz";

    /// <summary>
    /// Gets or sets symmetric signing key used by trusted authenticator.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}

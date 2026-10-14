namespace CynetAuth.Api.Security;

/// <summary>
/// Configuration options for JWT token issuance and validation.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the token issuer.
    /// </summary>
    public string Issuer { get; set; } = "cynetauth";

    /// <summary>
    /// Gets or sets the token audience.
    /// </summary>
    public string Audience { get; set; } = "chainballot";

    /// <summary>
    /// Gets or sets the symmetric signing key.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets token lifetime in minutes.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 30;
}

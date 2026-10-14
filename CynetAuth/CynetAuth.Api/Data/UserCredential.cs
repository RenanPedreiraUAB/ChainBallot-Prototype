namespace CynetAuth.Api.Data;

/// <summary>
/// Represents a local user account credential record.
/// </summary>
public sealed class UserCredential
{
    /// <summary>
    /// Gets or sets the stable account identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the normalized username used for case-insensitive lookup.
    /// </summary>
    public string NormalizedUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password hash bytes.
    /// </summary>
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the password salt bytes.
    /// </summary>
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets whether the account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the account creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

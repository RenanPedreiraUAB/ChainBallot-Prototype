namespace ElectionAuthorization.Api.Data;

/// <summary>
/// Maps trusted authenticator subject identifiers to virtual IDs.
/// </summary>
public sealed class VoterIdentityMapping
{
    /// <summary>
    /// Gets or sets internal record identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets trusted authenticator subject identifier.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets virtual identity assigned to the subject.
    /// </summary>
    public string Vid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

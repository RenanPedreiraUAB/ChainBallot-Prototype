namespace ElectionAuthorization.Api.Data;

/// <summary>
/// Represents an issued one-time authorization grant for vote submission.
/// </summary>
public sealed class AuthorizationGrantRecord
{
    /// <summary>
    /// Gets or sets grant identifier.
    /// </summary>
    public Guid GrantId { get; set; }

    /// <summary>
    /// Gets or sets election identifier.
    /// </summary>
    public string ElectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets VID associated to the grant.
    /// </summary>
    public string Vid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets grant issue timestamp in UTC.
    /// </summary>
    public DateTimeOffset IssuedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets grant expiration timestamp in UTC.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the grant has been consumed.
    /// </summary>
    public bool IsConsumed { get; set; }

    /// <summary>
    /// Gets or sets grant consumption timestamp in UTC.
    /// </summary>
    public DateTimeOffset? ConsumedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets client nonce associated with successful consumption.
    /// </summary>
    public string? SubmissionNonce { get; set; }
}

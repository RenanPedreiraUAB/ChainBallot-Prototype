namespace ElectionAuthorization.Api.Data;

/// <summary>
/// Represents an audit event emitted by the Election Authorization component.
/// </summary>
public sealed class AuthorizationAuditEvent
{
    /// <summary>
    /// Gets or sets audit event identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets event type label.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets hashed subject identifier for privacy-preserving traceability.
    /// </summary>
    public string SubjectHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets VID associated with the event.
    /// </summary>
    public string? Vid { get; set; }

    /// <summary>
    /// Gets or sets election identifier associated with the event.
    /// </summary>
    public string? ElectionId { get; set; }

    /// <summary>
    /// Gets or sets operation outcome label.
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional event detail payload.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets event timestamp in UTC.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; set; }
}

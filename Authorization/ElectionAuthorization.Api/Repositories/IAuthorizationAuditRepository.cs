using ElectionAuthorization.Api.Data;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Defines persistence operations for authorization audit events.
/// </summary>
public interface IAuthorizationAuditRepository
{
    /// <summary>
    /// Persists an authorization audit event.
    /// </summary>
    /// <param name="auditEvent">Audit event entity to store.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task AddAsync(AuthorizationAuditEvent auditEvent, CancellationToken cancellationToken);
}

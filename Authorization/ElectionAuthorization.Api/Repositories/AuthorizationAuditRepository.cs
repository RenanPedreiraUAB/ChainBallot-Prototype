using ElectionAuthorization.Api.Data;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Persists authorization audit events.
/// </summary>
public sealed class AuthorizationAuditRepository(AuthorizationDbContext dbContext) : IAuthorizationAuditRepository
{
    /// <inheritdoc />
    public async Task AddAsync(AuthorizationAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

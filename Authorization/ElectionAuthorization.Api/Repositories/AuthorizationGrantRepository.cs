using ElectionAuthorization.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Persists and updates one-time authorization grants.
/// </summary>
public sealed class AuthorizationGrantRepository(AuthorizationDbContext dbContext) : IAuthorizationGrantRepository
{
    /// <inheritdoc />
    public async Task AddAsync(AuthorizationGrantRecord grant, CancellationToken cancellationToken)
    {
        dbContext.Grants.Add(grant);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<AuthorizationGrantRecord?> GetAsync(Guid grantId, CancellationToken cancellationToken)
    {
        return dbContext.Grants.SingleOrDefaultAsync(x => x.GrantId == grantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(Guid grantId, string submissionNonce, DateTimeOffset consumedAtUtc, CancellationToken cancellationToken)
    {
        var rowsAffected = await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE authorization_grants
SET IsConsumed = 1,
    ConsumedAtUtc = {consumedAtUtc},
    SubmissionNonce = {submissionNonce}
WHERE GrantId = {grantId}
  AND IsConsumed = 0
  AND ExpiresAtUtc > {consumedAtUtc}
", cancellationToken);

        return rowsAffected == 1;
    }
}

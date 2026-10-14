using ElectionAuthorization.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Persists and queries subject to VID mappings.
/// </summary>
public sealed class VoterIdentityRepository(AuthorizationDbContext dbContext) : IVoterIdentityRepository
{
    /// <inheritdoc />
    public Task<VoterIdentityMapping?> GetBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        return dbContext.IdentityMappings.SingleOrDefaultAsync(x => x.Subject == subject, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(VoterIdentityMapping mapping, CancellationToken cancellationToken)
    {
        dbContext.IdentityMappings.Add(mapping);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

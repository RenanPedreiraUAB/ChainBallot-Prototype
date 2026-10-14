using CynetAuth.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CynetAuth.Api.Repositories;

/// <summary>
/// Persists and queries user credentials through the authentication database context.
/// </summary>
public sealed class UserCredentialRepository(AuthDbContext dbContext) : IUserCredentialRepository
{
    /// <inheritdoc />
    public Task<bool> ExistsByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken)
    {
        return dbContext.Users.AnyAsync(x => x.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UserCredential?> GetActiveByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken)
    {
        return dbContext.Users.SingleOrDefaultAsync(
            x => x.NormalizedUsername == normalizedUsername && x.IsActive,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(UserCredential user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

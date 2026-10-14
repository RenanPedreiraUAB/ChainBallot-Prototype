using ElectionAuthorization.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ElectionAuthorization.Api.Repositories;

/// <summary>
/// Persists and updates election-level eligibility and vote-consumption state.
/// </summary>
public sealed class ElectionAuthorizationRepository(AuthorizationDbContext dbContext) : IElectionAuthorizationRepository
{
    /// <inheritdoc />
    public Task<ElectionVoterAuthorization?> GetAsync(string electionId, string vid, CancellationToken cancellationToken)
    {
        return dbContext.ElectionAuthorizations.SingleOrDefaultAsync(
            x => x.ElectionId == electionId && x.Vid == vid,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(ElectionVoterAuthorization state, CancellationToken cancellationToken)
    {
        dbContext.ElectionAuthorizations.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ElectionVoterAuthorization> UpsertEligibilityAsync(
        string electionId,
        string vid,
        bool isEligible,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(electionId, vid, cancellationToken);
        if (existing is null)
        {
            var created = new ElectionVoterAuthorization
            {
                Id = Guid.NewGuid(),
                ElectionId = electionId,
                Vid = vid,
                IsEligible = isEligible,
                HasVoted = false,
                CreatedAtUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc
            };

            dbContext.ElectionAuthorizations.Add(created);
            await dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.IsEligible = isEligible;
        existing.UpdatedAtUtc = updatedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeVoteAsync(string electionId, string vid, DateTimeOffset consumedAtUtc, CancellationToken cancellationToken)
    {
        var rowsAffected = await dbContext.ElectionAuthorizations
            .Where(x => x.ElectionId == electionId && x.Vid == vid && x.IsEligible && !x.HasVoted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.HasVoted, true)
                    .SetProperty(x => x.VotedAtUtc, consumedAtUtc)
                    .SetProperty(x => x.UpdatedAtUtc, consumedAtUtc),
                cancellationToken);

        return rowsAffected == 1;
    }
}

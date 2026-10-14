using ChainBallot.PublicContracts;

namespace TransparencyAudit.Api.Services;

public interface IResultSnapshotService
{
    Task<VerifiedResultSnapshot?> GetSnapshotAsync(string electionId, CancellationToken cancellationToken);
}

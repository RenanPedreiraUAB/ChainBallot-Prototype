using ChainBallot.PublicContracts;

namespace ResultPortal.Web.Services;

public interface ITransparencyReadClient
{
    Task<TransparencyReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken);

    Task<TransparencyReadResult<VidVerificationResponse>> VerifyByVidAsync(string electionId, string vid, CancellationToken cancellationToken);
}

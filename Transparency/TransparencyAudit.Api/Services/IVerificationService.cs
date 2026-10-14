using ChainBallot.PublicContracts;

namespace TransparencyAudit.Api.Services;

public interface IVerificationService
{
    Task<VidVerificationResponse> VerifyVidAsync(string electionId, string vid, CancellationToken cancellationToken);
}

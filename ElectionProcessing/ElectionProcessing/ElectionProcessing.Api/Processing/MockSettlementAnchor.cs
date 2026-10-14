using System.Security.Cryptography;
using System.Text;
using ChainBallot.PublicContracts;
using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Processing;

public sealed class MockSettlementAnchor(IOptions<ProcessingOptions> options) : ISettlementAnchor
{
    public Task<SettlementReference> AnchorAsync(BatchAnchorRequest request, CancellationToken cancellationToken)
    {
        var canonical = $"{request.ElectionId}:{request.BatchId}:{request.BatchRoot}:{request.ProofArtifact.ProofDigest}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var signature = Convert.ToHexString(digest).ToLowerInvariant();

        var settlement = new SettlementReference(
            Network: options.Value.SettlementNetwork,
            ProgramId: options.Value.SettlementProgramId,
            ElectionId: request.ElectionId,
            TransactionSignature: signature,
            Slot: (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            BlockTimeUtc: DateTimeOffset.UtcNow,
            Commitment: options.Value.SettlementCommitment);

        return Task.FromResult(settlement);
    }
}

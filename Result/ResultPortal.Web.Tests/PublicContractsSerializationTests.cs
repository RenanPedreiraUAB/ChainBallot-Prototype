using System.Text.Json;
using ChainBallot.PublicContracts;

namespace ResultPortal.Web.Tests;

public sealed class PublicContractsSerializationTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BallotSubmissionPackage_RoundTrips_WithStableShape()
    {
        var value = new BallotSubmissionPackage(
            ElectionId: "election-1",
            Vid: "vid-1",
            BallotCommitment: "commit-1",
            SubmissionNonce: "nonce-1",
            BallotNullifier: "nullifier-1",
            EncryptedVotePayload: new EncryptedBallotPayload("AES-GCM", "nonce-b64", "tag-b64", "cipher-b64"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        var json = JsonSerializer.Serialize(value, WebJson);
        var copy = JsonSerializer.Deserialize<BallotSubmissionPackage>(json, WebJson);

        Assert.Contains("\"electionId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ballotNullifier\"", json, StringComparison.Ordinal);
        Assert.NotNull(copy);
        Assert.Equal(value, copy);
    }

    [Fact]
    public void VerificationAndResultContracts_RoundTrip_WithStableShape()
    {
        var settlement = new SettlementReference("localnet", "program-v2", "election-1", "tx-1", 10, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "finalized");
        var inclusion = new InclusionProofReference("batch-1", "root-1", "leaf-1", 0, "proof://1", "hash-1");
        var verification = new VidVerificationResponse("election-1", "vid-1", VerificationOutcome.Verified, "ok", settlement, inclusion, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "digest-1");
        var result = new VerifiedResultSnapshot("election-1", "result-digest", "Finalized", settlement, new Dictionary<string, long> { ["cand-a"] = 1 }, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        var verificationJson = JsonSerializer.Serialize(verification, WebJson);
        var resultJson = JsonSerializer.Serialize(result, WebJson);

        var verificationCopy = JsonSerializer.Deserialize<VidVerificationResponse>(verificationJson, WebJson);
        var resultCopy = JsonSerializer.Deserialize<VerifiedResultSnapshot>(resultJson, WebJson);

        Assert.Contains("\"outcome\"", verificationJson, StringComparison.Ordinal);
        Assert.Contains("\"resultDigest\"", resultJson, StringComparison.Ordinal);
        Assert.NotNull(verificationCopy);
        Assert.NotNull(resultCopy);
        Assert.Equal(verification.Outcome, verificationCopy!.Outcome);
        Assert.Equal(verification.Vid, verificationCopy.Vid);
        Assert.Equal(result.ElectionId, resultCopy!.ElectionId);
        Assert.Equal(result.ResultDigest, resultCopy.ResultDigest);
        Assert.Equal(result.CandidateTallies["cand-a"], resultCopy.CandidateTallies["cand-a"]);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChainBallot.PublicContracts;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Ballot commitment realization for the Voter Component (Architecture.md § 2.4.2): computes a
/// deterministic SHA-256 commitment over the ballot draft and its encrypted payload, binding the
/// submission so any later tampering with the anchored data is detectable against this committed value.
/// This is independent from the canonical <c>BallotNullifier</c> replay rule, which is derived separately.
/// </summary>
public sealed class Sha256BallotCommitmentService : IBallotCommitmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string ComputeCommitment(BallotSubmissionDraft draft, EncryptedBallotPayload encryptedPayload)
    {
        var payload = new
        {
            draft.ElectionId,
            draft.Vid,
            draft.CandidateId,
            draft.SubmissionNonce,
            draft.BallotNullifier,
            encryptedPayload.Algorithm,
            encryptedPayload.NonceBase64,
            encryptedPayload.TagBase64,
            encryptedPayload.CiphertextBase64
        };

        var serialized = JsonSerializer.Serialize(payload, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
using ChainBallot.PublicContracts;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Strategy boundary for ballot confidentiality (Architecture.md § 6.2). Any implementation must
/// return an authenticated-encryption envelope; callers never see or persist ballot plaintext.
/// </summary>
public interface IBallotEncryptionService
{
    /// <summary>Encrypts a ballot draft into an opaque, authenticated <see cref="EncryptedBallotPayload"/>.</summary>
    EncryptedBallotPayload Encrypt(BallotSubmissionDraft draft);
}
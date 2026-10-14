using ChainBallot.PublicContracts;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Strategy boundary for ballot commitment computation (Architecture.md § 2.4.2). The commitment binds
/// a submission to its encrypted payload so later tampering is detectable; it is distinct from the
/// canonical <c>BallotNullifier</c> replay-prevention rule.
/// </summary>
public interface IBallotCommitmentService
{
    /// <summary>Computes the ballot commitment for a draft and its already-encrypted payload.</summary>
    string ComputeCommitment(BallotSubmissionDraft draft, EncryptedBallotPayload encryptedPayload);
}
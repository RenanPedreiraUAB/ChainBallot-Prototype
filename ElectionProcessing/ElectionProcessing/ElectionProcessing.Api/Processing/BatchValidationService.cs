using ChainBallot.PublicContracts;
using System.Security.Cryptography;
using System.Text;

namespace ElectionProcessing.Api.Processing;

public sealed class BatchValidationService : IBatchValidationService
{
    public BatchValidationResult Validate(string electionId, IReadOnlyList<BallotSubmissionPackage> ballots, BatchValidationContext context)
    {
        var accepted = new List<BallotSubmissionPackage>(ballots.Count);
        var rejected = new List<RejectedBallot>();

        var batchCommitments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchNullifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ballot in ballots)
        {
            if (string.IsNullOrWhiteSpace(ballot.ElectionId) || !string.Equals(ballot.ElectionId, electionId, StringComparison.Ordinal))
            {
                rejected.Add(new RejectedBallot(ballot, "ElectionId mismatch."));
                continue;
            }

            if (!IsValidVid(ballot.Vid))
            {
                rejected.Add(new RejectedBallot(ballot, "Invalid VID format."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(ballot.BallotCommitment) || ballot.BallotCommitment.Length != 64 || !IsLowerHex(ballot.BallotCommitment))
            {
                rejected.Add(new RejectedBallot(ballot, "Invalid ballot commitment format."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(ballot.BallotNullifier))
            {
                rejected.Add(new RejectedBallot(ballot, "Missing ballot nullifier."));
                continue;
            }

            if (ballot.BallotNullifier.Length != 64 || !IsLowerHex(ballot.BallotNullifier))
            {
                rejected.Add(new RejectedBallot(ballot, "Invalid ballot nullifier format."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(ballot.SubmissionNonce))
            {
                rejected.Add(new RejectedBallot(ballot, "Missing submission nonce."));
                continue;
            }

            var expectedNullifier = ComputeNullifier(ballot.ElectionId, ballot.Vid, ballot.SubmissionNonce);
            if (!string.Equals(expectedNullifier, ballot.BallotNullifier, StringComparison.OrdinalIgnoreCase))
            {
                rejected.Add(new RejectedBallot(ballot, "Ballot nullifier does not match canonical derivation."));
                continue;
            }

            if (!IsValidEnvelope(ballot.EncryptedVotePayload, out var envelopeError))
            {
                rejected.Add(new RejectedBallot(ballot, envelopeError));
                continue;
            }

            if (!batchCommitments.Add(ballot.BallotCommitment))
            {
                rejected.Add(new RejectedBallot(ballot, "Duplicate commitment inside batch."));
                continue;
            }

            if (!batchNullifiers.Add(ballot.BallotNullifier))
            {
                rejected.Add(new RejectedBallot(ballot, "Duplicate ballot nullifier inside batch."));
                continue;
            }

            if (context.SeenCommitments.Contains(ballot.BallotCommitment))
            {
                rejected.Add(new RejectedBallot(ballot, "Commitment already processed in prior batch."));
                continue;
            }

            if (context.SeenNullifiers.Contains(ballot.BallotNullifier))
            {
                rejected.Add(new RejectedBallot(ballot, "Ballot nullifier already processed in prior batch."));
                continue;
            }

            accepted.Add(ballot);
        }

        return new BatchValidationResult(accepted, rejected);
    }

    /// <summary>Matches the shape produced by <c>CryptographicVidGenerator</c> in the Authorization component ("vid_" + lowercase hex).</summary>
    private static bool IsValidVid(string? vid)
    {
        const string prefix = "vid_";
        if (string.IsNullOrWhiteSpace(vid) || vid.Length <= prefix.Length || !vid.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return IsLowerHex(vid[prefix.Length..]);
    }

    private static bool IsValidEnvelope(EncryptedBallotPayload? envelope, out string error)
    {
        if (envelope is null)
        {
            error = "Missing encrypted ballot envelope.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Algorithm) || !SupportedEnvelopeAlgorithms.Contains(envelope.Algorithm))
        {
            error = "Unsupported or missing encryption algorithm.";
            return false;
        }

        if (!IsNonEmptyBase64(envelope.NonceBase64) || !IsNonEmptyBase64(envelope.TagBase64) || !IsNonEmptyBase64(envelope.CiphertextBase64))
        {
            error = "Malformed encrypted ballot envelope (invalid base64 field).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsNonEmptyBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static readonly HashSet<string> SupportedEnvelopeAlgorithms = new(StringComparer.Ordinal) { "A256GCM" };

    private static string ComputeNullifier(string electionId, string vid, string submissionNonce)
    {
        var input = $"{electionId}|{vid}|{submissionNonce}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var ch in value)
        {
            var isDigit = ch is >= '0' and <= '9';
            var isLowerHexLetter = ch is >= 'a' and <= 'f';
            if (!isDigit && !isLowerHexLetter)
            {
                return false;
            }
        }

        return true;
    }
}

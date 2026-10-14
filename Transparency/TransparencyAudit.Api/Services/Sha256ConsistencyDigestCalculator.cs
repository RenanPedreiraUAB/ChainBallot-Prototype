using System.Security.Cryptography;
using System.Text;
using TransparencyAudit.Api.Domain;

namespace TransparencyAudit.Api.Services;

public sealed class Sha256ConsistencyDigestCalculator : IConsistencyDigestCalculator
{
    public string ComputeElectionDigest(
        string electionId,
        string lifecycleState,
        string resultDigest,
        IReadOnlyDictionary<string, long> candidateTallies,
        IReadOnlyList<VidEvidenceEntry> vidEntries,
        string? metadataDigest,
        string? auditDigest)
    {
        var builder = new StringBuilder();
        builder.Append(electionId).Append('|')
            .Append(lifecycleState).Append('|')
            .Append(resultDigest).Append('|')
            .Append(metadataDigest ?? string.Empty).Append('|')
            .Append(auditDigest ?? string.Empty);

        foreach (var tally in candidateTallies.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(tally.Key).Append(':').Append(tally.Value);
        }

        foreach (var vidEntry in vidEntries.OrderBy(v => v.Vid, StringComparer.Ordinal))
        {
            builder.Append('|').Append(vidEntry.Vid)
                .Append(':').Append(vidEntry.BatchId)
                .Append(':').Append(vidEntry.BallotCommitment)
                .Append(':').Append(vidEntry.InclusionProofReference.MerkleRoot)
                .Append(':').Append(vidEntry.InclusionProofReference.LeafIndex)
                .Append(':').Append(vidEntry.ConsistencyDigest ?? string.Empty);
        }

        return ComputeSha256Hex(builder.ToString());
    }

    public string ComputeVidDigest(string electionId, string vid, string batchId, string ballotCommitment, string merkleRoot, int leafIndex)
    {
        var canonical = string.Join("|", electionId, vid, batchId, ballotCommitment, merkleRoot, leafIndex.ToString());
        return ComputeSha256Hex(canonical);
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

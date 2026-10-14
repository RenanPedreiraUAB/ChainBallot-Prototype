using System.Security.Cryptography;
using System.Text;

namespace ElectionProcessing.Api.Processing;

public sealed class BinarySha256MerkleTreeStrategy : ITreeStrategy
{
    public string Name => "BinarySha256Merkle";

    public TreeComputationResult BuildRoot(IReadOnlyList<string> commitments)
    {
        if (commitments.Count == 0)
        {
            throw new InvalidOperationException("Cannot build a Merkle tree with zero commitments.");
        }

        var layer = commitments
            .Select(c => NormalizeHex(c))
            .ToList();

        while (layer.Count > 1)
        {
            if (layer.Count % 2 == 1)
            {
                layer.Add(layer[^1]);
            }

            var next = new List<string>(layer.Count / 2);
            for (var i = 0; i < layer.Count; i += 2)
            {
                var combined = layer[i] + layer[i + 1];
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                next.Add(Convert.ToHexString(hash).ToLowerInvariant());
            }

            layer = next;
        }

        return new TreeComputationResult(layer[0], commitments.Count, "SHA-256");
    }

    private static string NormalizeHex(string input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? throw new InvalidOperationException("Commitment cannot be null or empty.")
            : input.Trim().ToLowerInvariant();
    }
}

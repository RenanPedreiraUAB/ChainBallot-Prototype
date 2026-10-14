using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>Default <see cref="IWatcherDigestCalculator"/> implementation; computes a SHA-256 digest over the canonicalized report content.</summary>
public sealed class Sha256WatcherDigestCalculator : IWatcherDigestCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public string ComputeDigest(WatcherAuditReport reportWithoutDigestOrSignature)
    {
        var serialized = JsonSerializer.Serialize(reportWithoutDigestOrSignature, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(serialized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

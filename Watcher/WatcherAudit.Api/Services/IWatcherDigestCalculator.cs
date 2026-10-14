using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>Computes a deterministic digest over a watcher report's content, prior to signing (Architecture.md § 3.10).</summary>
public interface IWatcherDigestCalculator
{
    /// <summary>Computes the report digest, excluding the digest/signature fields themselves.</summary>
    string ComputeDigest(WatcherAuditReport reportWithoutDigestOrSignature);
}

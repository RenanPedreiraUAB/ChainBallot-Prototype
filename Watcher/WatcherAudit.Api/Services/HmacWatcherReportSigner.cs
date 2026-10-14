using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WatcherAudit.Api.Application;

namespace WatcherAudit.Api.Services;

/// <summary>HMAC-based <see cref="IWatcherReportSigner"/> implementation using a configured signing key (Prototype.md § 4.10).</summary>
public sealed class HmacWatcherReportSigner : IWatcherReportSigner
{
    private readonly byte[] _key;

    public HmacWatcherReportSigner(IOptions<WatcherOptions> options)
    {
        var configuredKey = options.Value.ReportSigningKey;
        var effectiveKey = string.IsNullOrWhiteSpace(configuredKey)
            ? "watcher-dev-key"
            : configuredKey;

        _key = Encoding.UTF8.GetBytes(effectiveKey);
    }

    public string Sign(string digest)
    {
        var bytes = Encoding.UTF8.GetBytes(digest);
        using var hmac = new HMACSHA256(_key);
        var signature = hmac.ComputeHash(bytes);
        return Convert.ToHexString(signature).ToLowerInvariant();
    }
}

namespace WatcherAudit.Api.Services;

/// <summary>Signs a watcher report digest so tampering with a stored/exported report is detectable (Architecture.md § 3.10).</summary>
public interface IWatcherReportSigner
{
    /// <summary>Produces a signature over the given report digest.</summary>
    string Sign(string digest);
}

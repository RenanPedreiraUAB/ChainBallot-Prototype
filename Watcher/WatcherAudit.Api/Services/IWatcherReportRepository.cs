using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>Persistence boundary for signed Watcher audit reports.</summary>
public interface IWatcherReportRepository
{
    /// <summary>Persists a newly generated report.</summary>
    Task<WatcherAuditReport> SaveAsync(WatcherAuditReport report, CancellationToken cancellationToken);

    /// <summary>Retrieves a report by its id.</summary>
    Task<WatcherAuditReport?> GetByIdAsync(string reportId, CancellationToken cancellationToken);

    /// <summary>Retrieves the most recently generated report for an election.</summary>
    Task<WatcherAuditReport?> GetLatestByElectionAsync(string electionId, CancellationToken cancellationToken);

    /// <summary>Lists every report generated for an election.</summary>
    Task<IReadOnlyList<WatcherAuditReport>> ListByElectionAsync(string electionId, CancellationToken cancellationToken);
}

using System.Text.Json;
using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>Default, file-backed implementation of <see cref="IWatcherReportRepository"/> (Prototype.md § 4.10).</summary>
public sealed class FileBackedWatcherReportRepository : IWatcherReportRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileBackedWatcherReportRepository(string path)
    {
        _path = path;
    }

    public async Task<WatcherAuditReport> SaveAsync(WatcherAuditReport report, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var updated = items.Where(r => !string.Equals(r.ReportId, report.ReportId, StringComparison.OrdinalIgnoreCase)).ToList();
            updated.Add(report);
            await WriteAsync(updated, cancellationToken).ConfigureAwait(false);
            return report;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WatcherAuditReport?> GetByIdAsync(string reportId, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return items.FirstOrDefault(r => string.Equals(r.ReportId, reportId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WatcherAuditReport?> GetLatestByElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return items
                .Where(r => string.Equals(r.ElectionId, electionId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.GeneratedAtUtc)
                .FirstOrDefault();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<WatcherAuditReport>> ListByElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return items
                .Where(r => string.Equals(r.ElectionId, electionId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.GeneratedAtUtc)
                .ToArray();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<WatcherAuditReport>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var stream = File.OpenRead(_path);
        var items = await JsonSerializer.DeserializeAsync<List<WatcherAuditReport>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return items ?? [];
    }

    private async Task WriteAsync(List<WatcherAuditReport> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}

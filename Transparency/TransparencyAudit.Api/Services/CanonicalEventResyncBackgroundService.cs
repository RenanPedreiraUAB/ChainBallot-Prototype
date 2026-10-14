namespace TransparencyAudit.Api.Services;

/// <summary>
/// Background worker that consumes canonical resync-trigger events (Architecture.md § 8.1 "Read-Model
/// Drift") and reindexes the affected election's projection, so drift detected by an external source
/// can be surfaced and corrected without manual intervention.
/// </summary>
public sealed class CanonicalEventResyncBackgroundService : BackgroundService
{
    private readonly ICanonicalResyncEventSource _eventSource;
    private readonly IProjectionManagementService _projectionManagementService;
    private readonly ILogger<CanonicalEventResyncBackgroundService> _logger;

    public CanonicalEventResyncBackgroundService(
        ICanonicalResyncEventSource eventSource,
        IProjectionManagementService projectionManagementService,
        ILogger<CanonicalEventResyncBackgroundService> logger)
    {
        _eventSource = eventSource;
        _projectionManagementService = projectionManagementService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _eventSource.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var result = await _projectionManagementService.ReindexElectionAsync(evt.ElectionId, stoppingToken).ConfigureAwait(false);
                if (result is null)
                {
                    _logger.LogWarning(
                        "Canonical resync event skipped for election {ElectionId}. Reason={Reason}, Source={Source}",
                        evt.ElectionId,
                        evt.Reason,
                        evt.Source);
                    continue;
                }

                _logger.LogInformation(
                    "Canonical resync event processed for election {ElectionId}. Reason={Reason}, Source={Source}, IsStale={IsStale}",
                    evt.ElectionId,
                    evt.Reason,
                    evt.Source,
                    result.IsStale);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Canonical resync event failed for election {ElectionId}", evt.ElectionId);
            }
        }
    }
}

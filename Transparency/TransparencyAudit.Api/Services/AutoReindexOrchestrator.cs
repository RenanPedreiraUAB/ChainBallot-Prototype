using Microsoft.Extensions.Options;
using TransparencyAudit.Api.Application;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// Periodically reindexes a configured set of elections' projections from canonical/authoritative
/// sources, as a proactive complement to the event-driven <see cref="CanonicalEventResyncBackgroundService"/>.
/// </summary>
public sealed class AutoReindexOrchestrator : IAutoReindexOrchestrator
{
    private readonly IProjectionManagementService _projectionManagementService;
    private readonly IOptions<TransparencyOptions> _options;
    private readonly ILogger<AutoReindexOrchestrator> _logger;

    public AutoReindexOrchestrator(
        IProjectionManagementService projectionManagementService,
        IOptions<TransparencyOptions> options,
        ILogger<AutoReindexOrchestrator> logger)
    {
        _projectionManagementService = projectionManagementService;
        _options = options;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        if (!opts.AutoReindexEnabled)
        {
            return;
        }

        if (opts.AutoReindexElectionIds.Length == 0)
        {
            return;
        }

        foreach (var electionId in opts.AutoReindexElectionIds)
        {
            if (string.IsNullOrWhiteSpace(electionId))
            {
                continue;
            }

            try
            {
                var result = await _projectionManagementService.ReindexElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    _logger.LogWarning("Auto reindex skipped for election {ElectionId} because projection was not found.", electionId);
                    continue;
                }

                _logger.LogInformation(
                    "Auto reindex completed for election {ElectionId}. IsStale={IsStale}, ConsistencyDigest={ConsistencyDigest}",
                    electionId,
                    result.IsStale,
                    result.ConsistencyDigest);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto reindex failed for election {ElectionId}.", electionId);
            }
        }
    }
}

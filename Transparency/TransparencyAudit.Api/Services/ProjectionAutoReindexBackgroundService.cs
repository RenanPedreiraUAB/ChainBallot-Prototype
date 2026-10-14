using Microsoft.Extensions.Options;
using TransparencyAudit.Api.Application;

namespace TransparencyAudit.Api.Services;

public sealed class ProjectionAutoReindexBackgroundService : BackgroundService
{
    private readonly IAutoReindexOrchestrator _orchestrator;
    private readonly IOptions<TransparencyOptions> _options;
    private readonly ILogger<ProjectionAutoReindexBackgroundService> _logger;

    public ProjectionAutoReindexBackgroundService(
        IAutoReindexOrchestrator orchestrator,
        IOptions<TransparencyOptions> options,
        ILogger<ProjectionAutoReindexBackgroundService> logger)
    {
        _orchestrator = orchestrator;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (!opts.AutoReindexEnabled)
        {
            _logger.LogInformation("Auto reindex background service is disabled by configuration.");
            return;
        }

        var intervalSeconds = Math.Max(5, opts.AutoReindexIntervalSeconds);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        _logger.LogInformation("Auto reindex background service started with interval {IntervalSeconds}s.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _orchestrator.RunOnceAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

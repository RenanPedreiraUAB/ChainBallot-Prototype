using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Processing;

public sealed class ElectionProcessingWorker(
    IProcessingCoordinator coordinator,
    IOptions<ProcessingOptions> options,
    ILogger<ElectionProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await coordinator.RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled processing cycle error.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}

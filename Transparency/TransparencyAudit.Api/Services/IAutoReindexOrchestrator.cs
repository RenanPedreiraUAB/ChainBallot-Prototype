namespace TransparencyAudit.Api.Services;

public interface IAutoReindexOrchestrator
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}

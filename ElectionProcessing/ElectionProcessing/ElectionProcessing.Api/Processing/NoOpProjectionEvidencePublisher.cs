namespace ElectionProcessing.Api.Processing;

public sealed class NoOpProjectionEvidencePublisher : IProjectionEvidencePublisher
{
    public Task PublishBatchAnchoredAsync(ProjectionEvidenceBatch batch, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

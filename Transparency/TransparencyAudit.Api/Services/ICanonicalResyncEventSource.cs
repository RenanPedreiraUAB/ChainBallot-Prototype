namespace TransparencyAudit.Api.Services;

public interface ICanonicalResyncEventSource
{
    ValueTask PublishAsync(CanonicalResyncEvent @event, CancellationToken cancellationToken);

    IAsyncEnumerable<CanonicalResyncEvent> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed record CanonicalResyncEvent(
    string ElectionId,
    string Reason,
    string Source,
    string? CorrelationId,
    DateTimeOffset OccurredAtUtc);

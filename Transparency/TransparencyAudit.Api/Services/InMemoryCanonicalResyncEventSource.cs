using System.Threading.Channels;

namespace TransparencyAudit.Api.Services;

public sealed class InMemoryCanonicalResyncEventSource : ICanonicalResyncEventSource
{
    private readonly Channel<CanonicalResyncEvent> _channel = Channel.CreateUnbounded<CanonicalResyncEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask PublishAsync(CanonicalResyncEvent @event, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(@event, cancellationToken);

    public IAsyncEnumerable<CanonicalResyncEvent> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

using System.Text.Json;
using ChainBallot.PublicContracts;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Canonical runtime realization of the Message Broker publish side (Architecture.md § 3.5): publishes
/// the serialized <see cref="BallotSubmissionPackage"/> to the configured Kafka-compatible topic
/// (Redpanda in the local prototype, see Prototype.md § 4.5). The in-memory <see cref="IQueuePublisher"/>
/// implementation is retained only for unit/component tests, never for the demonstrated runtime path.
/// </summary>
public sealed class KafkaBallotQueuePublisher : IQueuePublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly VoterOptions _options;
    private readonly IProducer<Null, string> _producer;
    private bool _disposed;

    public KafkaBallotQueuePublisher(IOptions<VoterOptions> options)
    {
        _options = options.Value;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.Kafka.BootstrapServers,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    public async Task<QueueReceipt> PublishAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(package, JsonOptions);
        await _producer.ProduceAsync(
            _options.Kafka.Topic,
            new Message<Null, string> { Value = payload },
            cancellationToken);

        return new QueueReceipt(
            QueueItemId: Guid.NewGuid(),
            QueuedAtUtc: DateTimeOffset.UtcNow,
            QueueMode: VoterQueueModes.Kafka);
    }

    public IReadOnlyCollection<BallotSubmissionPackage> Snapshot() => Array.Empty<BallotSubmissionPackage>();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _producer.Dispose();
    }
}

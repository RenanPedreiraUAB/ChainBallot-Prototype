using System.Text.Json;
using ChainBallot.PublicContracts;
using Confluent.Kafka;
using ElectionProcessing.Api.Application;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Processing;

public sealed class KafkaBallotQueueAdapter : IBallotQueueIngress, IBallotQueueConsumer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<Null, string> _producer;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly ProcessingOptions _options;
    private bool _disposed;

    public KafkaBallotQueueAdapter(IOptions<ProcessingOptions> options)
    {
        _options = options.Value;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.Kafka.BootstrapServers,
            Acks = Acks.All
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.Kafka.BootstrapServers,
            GroupId = _options.Kafka.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        _consumer.Subscribe(_options.Kafka.Topic);
    }

    public int ApproximateDepth => 0;

    public async Task EnqueueAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(package, JsonOptions);
        await _producer.ProduceAsync(
            _options.Kafka.Topic,
            new Message<Null, string> { Value = payload },
            cancellationToken);
    }

    public Task<IReadOnlyList<BallotSubmissionPackage>> DrainAsync(CancellationToken cancellationToken)
    {
        var results = new List<BallotSubmissionPackage>();
        var timeout = TimeSpan.FromMilliseconds(_options.Kafka.ConsumeTimeoutMilliseconds);

        for (var i = 0; i < _options.Kafka.PollBatchSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConsumeResult<Ignore, string>? consumed;
            try
            {
                consumed = _consumer.Consume(timeout);
            }
            catch (ConsumeException)
            {
                break;
            }

            if (consumed is null || consumed.Message is null || string.IsNullOrWhiteSpace(consumed.Message.Value))
            {
                break;
            }

            BallotSubmissionPackage? package;
            try
            {
                package = JsonSerializer.Deserialize<BallotSubmissionPackage>(consumed.Message.Value, JsonOptions);
            }
            catch (JsonException)
            {
                _consumer.StoreOffset(consumed);
                continue;
            }

            if (package is not null)
            {
                results.Add(package);
            }

            _consumer.StoreOffset(consumed);
        }

        if (results.Count > 0)
        {
            _consumer.Commit();
        }

        return Task.FromResult<IReadOnlyList<BallotSubmissionPackage>>(results);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _consumer.Close();
        _consumer.Dispose();
        _producer.Dispose();
    }
}

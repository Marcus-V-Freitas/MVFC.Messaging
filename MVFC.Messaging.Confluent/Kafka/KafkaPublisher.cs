namespace MVFC.Messaging.Confluent.Kafka;

public sealed class KafkaPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaPublisher(string bootstrapServers, string topic)
    {
        _topic = topic;
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        var kafkaMessage = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = json
        };

        await _producer.ProduceAsync(_topic, kafkaMessage, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        var tasks = messages.Select(msg => PublishInternalAsync(msg, cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async ValueTask DisposeAsync()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
        await Task.CompletedTask;
    }
}

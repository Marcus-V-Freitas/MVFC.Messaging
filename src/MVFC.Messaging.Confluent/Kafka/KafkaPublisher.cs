namespace MVFC.Messaging.Confluent.Kafka;

public sealed class KafkaPublisher<T>(string bootstrapServers, string topic) 
    : MessagePublisherBase<T>, IAsyncDisposable
{
    private const int FlushTimeoutSeconds = 10;

    private readonly IProducer<string, string> _producer = CreateKafkaProducer(bootstrapServers);
    private readonly string _topic = topic;

    private static IProducer<string, string> CreateKafkaProducer(string bootstrapServers)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        return new ProducerBuilder<string, string>(config).Build();
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var kafkaMessage = CreateKafkaMessage(message);
        await _producer.ProduceAsync(_topic, kafkaMessage, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        var publishTasks = CreatePublishTasks(messages, cancellationToken);
        await Task.WhenAll(publishTasks);
    }

    private IEnumerable<Task> CreatePublishTasks(
        IEnumerable<T> messages,
        CancellationToken cancellationToken) =>
            messages.Select(message => PublishInternalAsync(message, cancellationToken));

    private static Message<string, string> CreateKafkaMessage(T message)
    {
        var messageValue = SerializeMessage(message);
        var messageKey = GenerateMessageKey();

        return new Message<string, string>
        {
            Key = messageKey,
            Value = messageValue
        };
    }

    private static string SerializeMessage(T message) =>
        JsonSerializer.Serialize(message);

    private static string GenerateMessageKey() =>
         Guid.CreateVersion7(DateTimeOffset.Now).ToString();

    public async ValueTask DisposeAsync()
    {
        FlushPendingMessages();
        _producer?.Dispose();
        await Task.CompletedTask;
    }

    private void FlushPendingMessages() =>
        _producer?.Flush(TimeSpan.FromSeconds(FlushTimeoutSeconds));
}
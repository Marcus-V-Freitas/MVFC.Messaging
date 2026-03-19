namespace MVFC.Messaging.RabbitMQ.Rabbit;

public sealed class RabbitMqPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queueName;

    private RabbitMqPublisher(IConnection connection, IChannel channel, string queueName)
    {
        _connection = connection;
        _channel = channel;
        _queueName = queueName;
    }

    public static async Task<RabbitMqPublisher<T>> CreateAsync(
        string connectionString,
        string queueName)
    {
        var connection = await CreateConnectionAsync(connectionString).ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        var publisher = new RabbitMqPublisher<T>(connection, channel, queueName);
        await publisher.ConfigureQueueAsync().ConfigureAwait(false);

        return publisher;
    }

    private static async Task<IConnection> CreateConnectionAsync(string connectionString)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        return await factory.CreateConnectionAsync().ConfigureAwait(false);
    }

    private async Task ConfigureQueueAsync()
    {
        await _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null).ConfigureAwait(false);
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var messageBody = SerializeMessage(message);
        var properties = CreatePersistentProperties();

        await PublishMessageAsync(messageBody, properties, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        foreach (var message in messages)
        {
            await PublishInternalAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private static byte[] SerializeMessage(T message)
    {
        var json = JsonSerializer.Serialize(message);
        return Encoding.UTF8.GetBytes(json);
    }

    private static BasicProperties CreatePersistentProperties() =>
        new()
        {
            Persistent = true,
        };

    private async Task PublishMessageAsync(
        byte[] messageBody,
        BasicProperties properties,
        CancellationToken cancellationToken)
    {
        await _channel.BasicPublishAsync(
            exchange: "",
            routingKey: _queueName,
            mandatory: false,
            basicProperties: properties,
            body: messageBody,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAndDisposeChannelAsync().ConfigureAwait(false);
        await CloseAndDisposeConnectionAsync().ConfigureAwait(false);
    }

    private async Task CloseAndDisposeChannelAsync()
    {
        await _channel.CloseAsync().ConfigureAwait(false);
        _channel.Dispose();
    }

    private async Task CloseAndDisposeConnectionAsync()
    {
        await _connection.CloseAsync().ConfigureAwait(false);
        _connection.Dispose();
    }
}

namespace MVFC.Messaging.RabbitMQ.Rabbit;

public sealed class RabbitMqPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;

    public RabbitMqPublisher(string connectionString, string queueName)
    {
        _queueName = queueName;
        _connection = CreateConnection(connectionString);
        _channel = _connection.CreateModel();

        ConfigureQueue();
    }

    private static IConnection CreateConnection(string connectionString)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        return factory.CreateConnection();
    }

    private void ConfigureQueue()
    {
        _channel.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    protected override Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var messageBody = SerializeMessage(message);
        var properties = CreatePersistentProperties();

        PublishMessage(messageBody, properties);

        return Task.CompletedTask;
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await PublishInternalAsync(message, cancellationToken);
        }
    }

    private static byte[] SerializeMessage(T message)
    {
        var json = JsonSerializer.Serialize(message);
        return Encoding.UTF8.GetBytes(json);
    }

    private IBasicProperties CreatePersistentProperties()
    {
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        return properties;
    }

    private void PublishMessage(byte[] messageBody, IBasicProperties properties)
    {
        _channel.BasicPublish(
            exchange: "",
            routingKey: _queueName,
            basicProperties: properties,
            body: messageBody);
    }

    public ValueTask DisposeAsync()
    {
        CloseAndDisposeChannel();
        CloseAndDisposeConnection();

        return ValueTask.CompletedTask;
    }

    private void CloseAndDisposeChannel()
    {
        _channel?.Close();
        _channel?.Dispose();
    }

    private void CloseAndDisposeConnection()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
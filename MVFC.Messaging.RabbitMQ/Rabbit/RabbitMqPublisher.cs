namespace MVFC.Messaging.RabbitMQ.Rabbit;

public sealed class RabbitMqPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;

    public RabbitMqPublisher(string connectionString, string queueName)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _queueName = queueName;

        _channel.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    protected override Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: "",
            routingKey: _queueName,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    protected override Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            PublishInternalAsync(message, cancellationToken).Wait(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }
}
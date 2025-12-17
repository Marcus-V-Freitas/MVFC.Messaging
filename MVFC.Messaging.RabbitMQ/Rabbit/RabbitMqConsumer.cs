namespace MVFC.Messaging.RabbitMQ.Rabbit;

public sealed class RabbitMqConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;
    private AsyncEventingBasicConsumer? _consumer;

    public RabbitMqConsumer(string connectionString, string queueName)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString), DispatchConsumersAsync = true };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _queueName = queueName;

        _channel.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _consumer = new AsyncEventingBasicConsumer(_channel);

        _consumer.Received += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<T>(json);

                if (Handler != null && message != null)
                {
                    await Handler(message, cancellationToken);
                }

                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception)
            {
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: _consumer);

        return Task.CompletedTask;
    }

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
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
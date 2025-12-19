namespace MVFC.Messaging.RabbitMQ.Rabbit;

public sealed class RabbitMqConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private const ushort PrefetchCount = 10;

    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;
    private AsyncEventingBasicConsumer? _consumer;

    public RabbitMqConsumer(string connectionString, string queueName)
    {
        _queueName = queueName;
        _connection = CreateConnection(connectionString);
        _channel = _connection.CreateModel();

        ConfigureQueue();
        ConfigureQos();
    }

    private static IConnection CreateConnection(string connectionString)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            DispatchConsumersAsync = true
        };

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

    private void ConfigureQos()
    {
        _channel.BasicQos(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false);
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.Received += (sender, ea) => HandleMessageReceivedAsync(ea, cancellationToken);

        StartConsuming();

        return Task.CompletedTask;
    }

    private async Task HandleMessageReceivedAsync(
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessMessageAsync(eventArgs, cancellationToken);
            AcknowledgeMessage(eventArgs.DeliveryTag);
        }
        catch (Exception)
        {
            RejectMessage(eventArgs.DeliveryTag);
        }
    }

    private async Task ProcessMessageAsync(
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var message = DeserializeMessage(eventArgs.Body);

        if (ShouldInvokeHandler(message))
        {
            await Handler!(message!, cancellationToken);
        }
    }

    private static T? DeserializeMessage(ReadOnlyMemory<byte> messageBody)
    {
        var bodyArray = messageBody.ToArray();
        var json = Encoding.UTF8.GetString(bodyArray);
        return JsonSerializer.Deserialize<T>(json);
    }

    private bool ShouldInvokeHandler(T? message)
    {
        return Handler is not null && message is not null;
    }

    private void AcknowledgeMessage(ulong deliveryTag)
    {
        _channel.BasicAck(deliveryTag: deliveryTag, multiple: false);
    }

    private void RejectMessage(ulong deliveryTag)
    {
        _channel.BasicNack(deliveryTag: deliveryTag, multiple: false, requeue: true);
    }

    private void StartConsuming()
    {
        _channel.BasicConsume(
            queue: _queueName,
            autoAck: false,
            consumer: _consumer);
    }

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
namespace MVFC.Messaging.RabbitMQ.Rabbit;

public static class RabbitMqConsumer
{
    public static async Task<RabbitMqConsumer<T>> CreateAsync<T>(
        string connectionString,
        string queueName)
    {
        var connection = await CreateConnectionAsync(connectionString).ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        var consumer = new RabbitMqConsumer<T>(connection, channel, queueName);
        await consumer.ConfigureQueueAsync().ConfigureAwait(false);
        await consumer.ConfigureQosAsync().ConfigureAwait(false);

        return consumer;
    }

    private static async Task<IConnection> CreateConnectionAsync(string connectionString)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString)
        };

        return await factory.CreateConnectionAsync().ConfigureAwait(false);
    }
}

public sealed class RabbitMqConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private const ushort PREFETCH_COUNT = 10;

    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queueName;
    private AsyncEventingBasicConsumer? _consumer;

    internal RabbitMqConsumer(IConnection connection, IChannel channel, string queueName)
    {
        _connection = connection;
        _channel = channel;
        _queueName = queueName;
    }

    internal async Task ConfigureQueueAsync()
    {
        await _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null).ConfigureAwait(false);
    }

    internal async Task ConfigureQosAsync()
    {
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PREFETCH_COUNT,
            global: false).ConfigureAwait(false);
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.ReceivedAsync += (sender, ea) => HandleMessageReceivedAsync(ea, cancellationToken);

        await StartConsumingAsync().ConfigureAwait(false);
    }

    private async Task HandleMessageReceivedAsync(
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessMessageAsync(eventArgs, cancellationToken).ConfigureAwait(false);
            await AcknowledgeMessageAsync(eventArgs.DeliveryTag).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await RejectMessageAsync(eventArgs.DeliveryTag).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessageAsync(
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var message = DeserializeMessage(eventArgs.Body);

        if (ShouldInvokeHandler(message))
        {
            await Handler!(message!, cancellationToken).ConfigureAwait(false);
        }
    }

    private static T? DeserializeMessage(ReadOnlyMemory<byte> messageBody)
    {
        var bodyArray = messageBody.ToArray();
        var json = Encoding.UTF8.GetString(bodyArray);
        return JsonSerializer.Deserialize<T>(json);
    }

    private bool ShouldInvokeHandler(T? message) =>
        Handler is not null && message is not null;

    private async Task AcknowledgeMessageAsync(ulong deliveryTag) =>
        await _channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false).ConfigureAwait(false);

    private async Task RejectMessageAsync(ulong deliveryTag) =>
        await _channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: true).ConfigureAwait(false);

    private async Task StartConsumingAsync()
    {
        await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: _consumer!).ConfigureAwait(false);
    }

    protected override Task StopInternalAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

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

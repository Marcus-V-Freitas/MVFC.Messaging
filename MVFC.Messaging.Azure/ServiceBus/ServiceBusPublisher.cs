namespace MVFC.Messaging.Azure.ServiceBus;

public sealed class ServiceBusPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusPublisher(string connectionString, string queueOrTopicName)
    {
        var clientOptions = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp
        };

        _client = new ServiceBusClient(connectionString, clientOptions);
        _sender = _client.CreateSender(queueOrTopicName);
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        var busMessage = new ServiceBusMessage(json);
        await _sender.SendMessageAsync(busMessage, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        using var messageBatch = await _sender.CreateMessageBatchAsync(cancellationToken);

        foreach (var message in messages)
        {
            var json = JsonSerializer.Serialize(message);
            messageBatch.TryAddMessage(new ServiceBusMessage(json));
        }

        await _sender.SendMessagesAsync(messageBatch, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
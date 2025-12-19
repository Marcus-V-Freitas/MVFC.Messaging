namespace MVFC.Messaging.Azure.ServiceBus;

public sealed class ServiceBusPublisher<T> 
    : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusPublisher(string connectionString, string queueOrTopicName)
    {
        _client = CreateServiceBusClient(connectionString);
        _sender = _client.CreateSender(queueOrTopicName);
    }

    private static ServiceBusClient CreateServiceBusClient(string connectionString)
    {
        var clientOptions = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp
        };

        return new ServiceBusClient(connectionString, clientOptions);
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var busMessage = CreateServiceBusMessage(message);
        await _sender.SendMessageAsync(busMessage, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        using var messageBatch = await _sender.CreateMessageBatchAsync(cancellationToken);

        AddMessagesToBatch(messages, messageBatch);

        await _sender.SendMessagesAsync(messageBatch, cancellationToken);
    }

    private static void AddMessagesToBatch(IEnumerable<T> messages, ServiceBusMessageBatch messageBatch)
    {
        foreach (var message in messages)
        {
            var busMessage = CreateServiceBusMessage(message);

            if (!messageBatch.TryAddMessage(busMessage))
            {
                throw new InvalidOperationException(
                    $"Message could not be added to batch. Batch size limit reached.");
            }
        }
    }

    private static ServiceBusMessage CreateServiceBusMessage(T message)
    {
        var json = SerializeMessage(message);
        return new ServiceBusMessage(json);
    }

    private static string SerializeMessage(T message) =>
        JsonSerializer.Serialize(message);

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
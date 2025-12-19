namespace MVFC.Messaging.Azure.ServiceBus;

public sealed class ServiceBusConsumer<T> 
    : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;

    public ServiceBusConsumer(string connectionString, string queueOrTopicName)
    {
        _client = CreateServiceBusClient(connectionString);
        _processor = CreateServiceBusProcessor(queueOrTopicName);
        ConfigureProcessorHandlers();
    }

    private static ServiceBusClient CreateServiceBusClient(string connectionString)
    {
        var clientOptions = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp
        };

        return new ServiceBusClient(connectionString, clientOptions);
    }

    private ServiceBusProcessor CreateServiceBusProcessor(string queueOrTopicName)
    {
        var processorOptions = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
        };

        return _client.CreateProcessor(queueOrTopicName, processorOptions);
    }

    private void ConfigureProcessorHandlers()
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var message = DeserializeMessage(args.Message);

        if (ShouldInvokeHandler(message))
        {
            await Handler!(message!, args.CancellationToken);
        }

        await CompleteMessageAsync(args);
    }

    private static T? DeserializeMessage(ServiceBusReceivedMessage receivedMessage)
    {
        var json = receivedMessage.Body.ToString();
        return JsonSerializer.Deserialize<T>(json);
    }

    private bool ShouldInvokeHandler(T? message)
    {
        return Handler is not null && message is not null;
    }

    private static async Task CompleteMessageAsync(ProcessMessageEventArgs args) =>
        await args.CompleteMessageAsync(args.Message);

    private static Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        Console.WriteLine($"Error: {args.Exception}");
        return Task.CompletedTask;
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken) =>
        await _processor.StartProcessingAsync(cancellationToken);

    protected override async Task StopInternalAsync(CancellationToken cancellationToken) =>
        await _processor.StopProcessingAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _client.DisposeAsync();
    }
}
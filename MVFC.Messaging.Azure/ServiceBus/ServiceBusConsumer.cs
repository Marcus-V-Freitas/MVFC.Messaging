namespace MVFC.Messaging.Azure.ServiceBus;

public sealed class ServiceBusConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;

    public ServiceBusConsumer(string connectionString, string queueOrTopicName)
    {
        var clientOptions = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp
        };

        _client = new ServiceBusClient(connectionString, clientOptions);

        var processorOptions = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
        };

        _processor = _client.CreateProcessor(queueOrTopicName, processorOptions);
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _processor.ProcessMessageAsync += async args =>
        {
            var json = args.Message.Body.ToString();
            var message = JsonSerializer.Deserialize<T>(json);

            if (Handler != null && message != null)
            {
                await Handler(message, args.CancellationToken);
            }

            await args.CompleteMessageAsync(args.Message);
        };

        _processor.ProcessErrorAsync += args =>
        {
            Console.WriteLine($"Error: {args.Exception}");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(cancellationToken);
    }

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _client.DisposeAsync();
    }
}
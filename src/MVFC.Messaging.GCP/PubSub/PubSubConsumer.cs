namespace MVFC.Messaging.GCP.PubSub;

public sealed class PubSubConsumer<T>(string projectId, string subscriptionId) : MessageConsumerBase<T>, IAsyncDisposable
{
    private const int StartupDelayMilliseconds = 1000;

    private readonly SubscriberClient _subscriber = CreateSubscriberClient(projectId, subscriptionId);
    private Task? _subscriberTask;

    private static SubscriberClient CreateSubscriberClient(string projectId, string subscriptionId)
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);

        return new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.Build();
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _subscriberTask = _subscriber.StartAsync(HandleMessageAsync);
        await AwaitSubscriberInitializationAsync(cancellationToken);
    }

    private async Task<SubscriberClient.Reply> HandleMessageAsync(
        PubsubMessage pubsubMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ProcessMessageAsync(pubsubMessage, cancellationToken);
        }
        catch (Exception)
        {
            return SubscriberClient.Reply.Nack;
        }
    }

    private async Task<SubscriberClient.Reply> ProcessMessageAsync(
        PubsubMessage pubsubMessage,
        CancellationToken cancellationToken)
    {
        var message = DeserializeMessage(pubsubMessage);

        if (ShouldInvokeHandler(message))
        {
            await Handler!(message!, cancellationToken);
        }

        return SubscriberClient.Reply.Ack;
    }

    private static T? DeserializeMessage(PubsubMessage pubsubMessage)
    {
        var messageData = pubsubMessage.Data.ToStringUtf8();
        return JsonSerializer.Deserialize<T>(messageData);
    }

    private bool ShouldInvokeHandler(T? message)
    {
        return Handler is not null && message is not null;
    }

    private static async Task AwaitSubscriberInitializationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(StartupDelayMilliseconds, cancellationToken);
    }

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _subscriber.StopAsync(cancellationToken);

        if (_subscriberTask is not null)
        {
            await AwaitSubscriberTaskCompletionAsync();
        }
    }

    private async Task AwaitSubscriberTaskCompletionAsync()
    {
        try
        {
            await _subscriberTask!;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation exceptions
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopSubscriberAsync();

        if (_subscriberTask is not null)
        {
            await AwaitSubscriberTaskCompletionSafelyAsync();
        }
    }

    private async Task StopSubscriberAsync()
    {
        await _subscriber.StopAsync(CancellationToken.None);
    }

    private async Task AwaitSubscriberTaskCompletionSafelyAsync()
    {
        try
        {
            await _subscriberTask!;
        }
        catch
        {
            // Ignore exceptions during disposal
        }
    }
}
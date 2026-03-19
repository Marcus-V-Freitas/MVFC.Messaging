namespace MVFC.Messaging.GCP.PubSub;

public sealed class PubSubConsumer<T>(string projectId, string subscriptionId) : MessageConsumerBase<T>, IAsyncDisposable
{
    private const int STARTUP_DELAY_MILLISECONDS = 1000;

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
        await AwaitSubscriberInitializationAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SubscriberClient.Reply> HandleMessageAsync(
        PubsubMessage pubsubMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ProcessMessageAsync(pubsubMessage, cancellationToken).ConfigureAwait(false);
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
            await Handler!(message!, cancellationToken).ConfigureAwait(false);
        }

        return SubscriberClient.Reply.Ack;
    }

    private static T? DeserializeMessage(PubsubMessage pubsubMessage)
    {
        var messageData = pubsubMessage.Data.ToStringUtf8();
        return JsonSerializer.Deserialize<T>(messageData);
    }

    private bool ShouldInvokeHandler(T? message) =>
        Handler is not null && message is not null;

    private static async Task AwaitSubscriberInitializationAsync(CancellationToken cancellationToken) =>
        await Task.Delay(STARTUP_DELAY_MILLISECONDS, cancellationToken).ConfigureAwait(false);

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _subscriber.StopAsync(cancellationToken).ConfigureAwait(false);

        if (_subscriberTask is not null)
        {
            await AwaitSubscriberTaskCompletionAsync().ConfigureAwait(false);
        }
    }

    private async Task AwaitSubscriberTaskCompletionAsync()
    {
        try
        {
            await _subscriberTask!.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation exceptions
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopSubscriberAsync().ConfigureAwait(false);

        if (_subscriberTask is not null)
        {
            await AwaitSubscriberTaskCompletionSafelyAsync().ConfigureAwait(false);
        }
    }

    private async Task StopSubscriberAsync() =>
        await _subscriber.StopAsync(CancellationToken.None).ConfigureAwait(false);

    private async Task AwaitSubscriberTaskCompletionSafelyAsync()
    {
        try
        {
            await _subscriberTask!.ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions during disposal
        }
    }
}

namespace MVFC.Messaging.GCP.PubSub;

public sealed class PubSubConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly SubscriberClient _subscriber;
    private Task? _subscriberTask;

    public PubSubConsumer(string projectId, string subscriptionId)
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);

        _subscriber = new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.Build();
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _subscriberTask = _subscriber.StartAsync(async (msg, ct) =>
        {
            try
            {
                var message = System.Text.Json.JsonSerializer.Deserialize<T>(msg.Data.ToStringUtf8());
                if (Handler != null && message != null)
                {
                    await Handler(message, ct);
                }
                return SubscriberClient.Reply.Ack;
            }
            catch (Exception)
            {
                return SubscriberClient.Reply.Nack;
            }
        });

        // Aguardar um momento para garantir que o subscriber iniciou
        await Task.Delay(1000, cancellationToken);
    }

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _subscriber.StopAsync(cancellationToken);

        if (_subscriberTask != null)
        {
            try
            {
                await _subscriberTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _subscriber.StopAsync(CancellationToken.None);
        if (_subscriberTask != null)
        {
            try
            {
                await _subscriberTask;
            }
            catch { }
        }
    }
}
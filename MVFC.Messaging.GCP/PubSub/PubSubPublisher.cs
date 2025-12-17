namespace MVFC.Messaging.GCP.PubSub;

public sealed class PubSubPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly PublisherClient _publisher;

    public PubSubPublisher(string projectId, string topicId)
    {
        var topicName = TopicName.FromProjectTopic(projectId, topicId);

        _publisher = new PublisherClientBuilder
        {
            TopicName = topicName,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.Build();
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(message);
        await _publisher.PublishAsync(json);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        var tasks = messages.Select(msg => PublishInternalAsync(msg, cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async ValueTask DisposeAsync()
    {
        await _publisher.ShutdownAsync(CancellationToken.None);
    }
}
namespace MVFC.Messaging.GCP.PubSub;

public sealed class PubSubPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly PublisherClient _publisher;

    public PubSubPublisher(string projectId, string topicId)
    {
        _publisher = CreatePublisherClient(projectId, topicId);
    }

    private static PublisherClient CreatePublisherClient(string projectId, string topicId)
    {
        var topicName = TopicName.FromProjectTopic(projectId, topicId);

        return new PublisherClientBuilder
        {
            TopicName = topicName,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
        }.Build();
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var messageData = SerializeMessage(message);
        await _publisher.PublishAsync(messageData).ConfigureAwait(false);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        var publishTasks = CreatePublishTasks(messages, cancellationToken);
        await Task.WhenAll(publishTasks).ConfigureAwait(false);
    }

    private IEnumerable<Task> CreatePublishTasks(
        IEnumerable<T> messages,
        CancellationToken cancellationToken) =>
            messages.Select(message => PublishInternalAsync(message, cancellationToken));

    private static string SerializeMessage(T message) =>
        JsonSerializer.Serialize(message);

    public async ValueTask DisposeAsync() =>
        await ShutdownPublisherAsync().ConfigureAwait(false);

    private async Task ShutdownPublisherAsync() =>
        await _publisher.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
}

namespace MVFC.Messaging.GCP.PubSub;

public sealed class PubSubPublisher<T>(string projectId, string topicId) : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly PublisherClient _publisher = CreatePublisherClient(projectId, topicId);

    private static PublisherClient CreatePublisherClient(string projectId, string topicId)
    {
        var topicName = TopicName.FromProjectTopic(projectId, topicId);

        return new PublisherClientBuilder
        {
            TopicName = topicName,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.Build();
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var messageData = SerializeMessage(message);
        await _publisher.PublishAsync(messageData);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        var publishTasks = CreatePublishTasks(messages, cancellationToken);
        await Task.WhenAll(publishTasks);
    }

    private IEnumerable<Task> CreatePublishTasks(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        return messages.Select(message => PublishInternalAsync(message, cancellationToken));
    }

    private static string SerializeMessage(T message)
    {
        return JsonSerializer.Serialize(message);
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownPublisherAsync();
    }

    private async Task ShutdownPublisherAsync()
    {
        await _publisher.ShutdownAsync(CancellationToken.None);
    }
}
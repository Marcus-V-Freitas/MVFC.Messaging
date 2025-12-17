namespace MVFC.Messaging.AWS.SQS;

public sealed class SqsPublisher<T>(IAmazonSQS sqsClient, string queueUrl) : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly IAmazonSQS _sqsClient = sqsClient;
    private readonly string _queueUrl = queueUrl;

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = json
        };

        await _sqsClient.SendMessageAsync(request, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        var entries = messages.Select((msg, index) => new SendMessageBatchRequestEntry
        {
            Id = index.ToString(),
            MessageBody = JsonSerializer.Serialize(msg)
        }).ToList();

        var request = new SendMessageBatchRequest
        {
            QueueUrl = _queueUrl,
            Entries = entries
        };

        await _sqsClient.SendMessageBatchAsync(request, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _sqsClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
namespace MVFC.Messaging.AWS.SQS;

public sealed class SqsPublisher<T>(IAmazonSQS sqsClient, string queueUrl)
    : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly IAmazonSQS _sqsClient = sqsClient;
    private readonly string _queueUrl = queueUrl;

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var request = CreateSendMessageRequest(message);
        await _sqsClient.SendMessageAsync(request, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        var request = CreateSendMessageBatchRequest(messages);
        await _sqsClient.SendMessageBatchAsync(request, cancellationToken);
    }

    private SendMessageRequest CreateSendMessageRequest(T message)
    {
        var messageBody = SerializeMessage(message);

        return new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = messageBody
        };
    }

    private SendMessageBatchRequest CreateSendMessageBatchRequest(IEnumerable<T> messages)
    {
        var entries = CreateBatchEntries(messages);

        return new SendMessageBatchRequest
        {
            QueueUrl = _queueUrl,
            Entries = entries
        };
    }

    private List<SendMessageBatchRequestEntry> CreateBatchEntries(IEnumerable<T> messages) =>
        [.. messages.Select(CreateBatchEntry)];

    private SendMessageBatchRequestEntry CreateBatchEntry(T message, int index)
    {
        var messageBody = SerializeMessage(message);

        return new SendMessageBatchRequestEntry
        {
            Id = index.ToString(),
            MessageBody = messageBody
        };
    }

    private static string SerializeMessage(T message) =>
        JsonSerializer.Serialize(message);

    public ValueTask DisposeAsync()
    {
        _sqsClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
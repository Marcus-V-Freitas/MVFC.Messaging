namespace MVFC.Messaging.AWS.SQS;

public sealed class SqsConsumer<T>(IAmazonSQS sqsClient, string queueUrl)
    : MessageConsumerBase<T>, IAsyncDisposable
{
    private const int MAX_MESSAGES_PER_REQUEST = 10;
    private const int WAIT_TIME_SECONDS = 5;

    private readonly IAmazonSQS _sqsClient = sqsClient;
    private readonly string _queueUrl = queueUrl;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = Task.Run(() => ExecutePollingLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ExecutePollingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAndProcessMessagesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAndProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var request = CreateReceiveMessageRequest();
        var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken).ConfigureAwait(false);

        foreach (var sqsMessage in response.Messages)
        {
            await ProcessSingleMessageAsync(sqsMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    private ReceiveMessageRequest CreateReceiveMessageRequest()
    {
        return new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = MAX_MESSAGES_PER_REQUEST,
            WaitTimeSeconds = WAIT_TIME_SECONDS
        };
    }

    private async Task ProcessSingleMessageAsync(Message sqsMessage, CancellationToken cancellationToken)
    {
        try
        {
            var message = DeserializeMessage(sqsMessage.Body);

            if (ShouldInvokeHandler(message))
            {
                await Handler!(message!, cancellationToken).ConfigureAwait(false);
            }

            await DeleteMessageAsync(sqsMessage.ReceiptHandle, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Log or handle processing errors as needed.
        }
    }

    private static T? DeserializeMessage(string messageBody) =>
        JsonSerializer.Deserialize<T>(messageBody);

    private bool ShouldInvokeHandler(T? message) =>
        Handler is not null && message is not null;

    private async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken) => 
        await _sqsClient.DeleteMessageAsync(_queueUrl, receiptHandle, cancellationToken).ConfigureAwait(false);

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _cts!.CancelAsync().ConfigureAwait(false);

        if (_pollingTask is not null)
        {
            await AwaitPollingTaskCompletionAsync().ConfigureAwait(false);
        }

        _cts?.Dispose();
    }

    private async Task AwaitPollingTaskCompletionAsync()
    {
        try
        {
            await _pollingTask!.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    public ValueTask DisposeAsync()
    {
        _sqsClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}

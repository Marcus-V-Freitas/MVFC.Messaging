namespace MVFC.Messaging.AWS.SQS;

public sealed class SqsConsumer<T>(IAmazonSQS sqsClient, string queueUrl) : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly IAmazonSQS _sqsClient = sqsClient;
    private readonly string _queueUrl = queueUrl;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _pollingTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 5
                    };

                    var response = await _sqsClient.ReceiveMessageAsync(request, _cts.Token);

                    foreach (var sqsMessage in response.Messages)
                    {
                        try
                        {
                            var message = JsonSerializer.Deserialize<T>(sqsMessage.Body);

                            if (Handler != null && message != null)
                            {
                                await Handler(message, _cts.Token);
                            }

                            await _sqsClient.DeleteMessageAsync(_queueUrl, sqsMessage.ReceiptHandle, _cts.Token);
                        }
                        catch (Exception)
                        {
                            // Log error - mensagem volta para fila após visibility timeout
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_pollingTask != null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
                // Esperado
            }
        }

        _cts?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        _sqsClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
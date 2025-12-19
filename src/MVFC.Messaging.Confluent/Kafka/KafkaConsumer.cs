namespace MVFC.Messaging.Confluent.Kafka;

public sealed class KafkaConsumer<T>(string bootstrapServers, string topic, string groupId) 
    : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly IConsumer<string, string> _consumer = CreateKafkaConsumer(bootstrapServers, groupId);
    private readonly string _topic = topic;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    private static IConsumer<string, string> CreateKafkaConsumer(string bootstrapServers, string groupId)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(_topic);
        _consumeTask = Task.Run(() => ExecuteConsumeLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ExecuteConsumeLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAndProcessMessageAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log or handle exceptions as needed
            }
        }
    }

    private async Task ConsumeAndProcessMessageAsync(CancellationToken cancellationToken)
    {
        var consumeResult = _consumer.Consume(cancellationToken);

        if (IsValidConsumeResult(consumeResult))
        {
            await ProcessMessageAsync(consumeResult, cancellationToken);
            CommitOffset(consumeResult);
        }
    }

    private static bool IsValidConsumeResult(ConsumeResult<string, string>? consumeResult) =>
        consumeResult?.Message?.Value is not null;

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        var message = DeserializeMessage(consumeResult.Message.Value);

        if (ShouldInvokeHandler(message))
        {
            await Handler!(message!, cancellationToken);
        }
    }

    private static T? DeserializeMessage(string messageValue) =>
        JsonSerializer.Deserialize<T>(messageValue);

    private bool ShouldInvokeHandler(T? message) =>
        Handler is not null && message is not null;

    private void CommitOffset(ConsumeResult<string, string> consumeResult) => 
        _consumer.Commit(consumeResult);

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts!.CancelAsync();

        if (_consumeTask is not null)
        {
            await AwaitConsumeTaskCompletionAsync();
        }

        CloseAndDisposeConsumer();
        _cts?.Dispose();
    }

    private async Task AwaitConsumeTaskCompletionAsync()
    {
        try
        {
            await _consumeTask!;
        }
        catch
        {
            // Ignore exceptions during disposal
        }
    }

    private void CloseAndDisposeConsumer()
    {
        _consumer?.Close();
        _consumer?.Dispose();
    }
}
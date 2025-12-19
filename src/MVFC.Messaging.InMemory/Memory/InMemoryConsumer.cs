namespace MVFC.Messaging.InMemory.Memory;

public sealed class InMemoryConsumer<T>(Channel<T> channel) : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly Channel<T> _channel = channel;
    private Task? _consumerTask;
    private CancellationTokenSource? _cts;

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTask = Task.Run(() => ExecuteConsumerLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ExecuteConsumerLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in ReadMessagesAsync(cancellationToken))
        {
            await ProcessMessageAsync(message, cancellationToken);
        }
    }

    private IAsyncEnumerable<T> ReadMessagesAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(T message, CancellationToken cancellationToken)
    {
        if (ShouldInvokeHandler())
        {
            await Handler!(message, cancellationToken);
        }
    }

    private bool ShouldInvokeHandler()
    {
        return Handler is not null;
    }

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _cts!.CancelAsync();

        if (_consumerTask is not null)
        {
            await AwaitConsumerTaskCompletionAsync();
        }
    }

    private async Task AwaitConsumerTaskCompletionAsync()
    {
        try
        {
            await _consumerTask!;
        }
        catch (OperationCanceledException)
        {
            // Swallow cancellation exceptions
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts!.CancelAsync();

        if (_consumerTask is not null)
        {
            await AwaitConsumerTaskCompletionSafelyAsync();
        }

        _cts?.Dispose();
    }

    private async Task AwaitConsumerTaskCompletionSafelyAsync()
    {
        try
        {
            await _consumerTask!;
        }
        catch
        {
            // Swallow exceptions during disposal
        }
    }
}
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
        await foreach (var message in ReadMessagesAsync(cancellationToken).ConfigureAwait(false))
        {
            await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private IAsyncEnumerable<T> ReadMessagesAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    private async Task ProcessMessageAsync(T message, CancellationToken cancellationToken)
    {
        if (ShouldInvokeHandler())
        {
            await Handler!(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool ShouldInvokeHandler() =>
        Handler is not null;

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        await _cts!.CancelAsync().ConfigureAwait(false);

        if (_consumerTask is not null)
        {
            await AwaitConsumerTaskCompletionAsync().ConfigureAwait(false);
        }
    }

    private async Task AwaitConsumerTaskCompletionAsync()
    {
        try
        {
            await _consumerTask!.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Swallow cancellation exceptions
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts!.CancelAsync().ConfigureAwait(false);

        if (_consumerTask is not null)
        {
            await AwaitConsumerTaskCompletionSafelyAsync().ConfigureAwait(false);
        }

        _cts?.Dispose();
    }

    private async Task AwaitConsumerTaskCompletionSafelyAsync()
    {
        try
        {
            await _consumerTask!.ConfigureAwait(false);
        }
        catch
        {
            // Swallow exceptions during disposal
        }
    }
}

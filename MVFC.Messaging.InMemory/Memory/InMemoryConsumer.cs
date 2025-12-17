namespace MVFC.Messaging.InMemory.Memory;

public sealed class InMemoryConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly Channel<T> _channel;
    private Task? _consumerTask;
    private CancellationTokenSource? _cts;

    public InMemoryConsumer(Channel<T> channel)
    {
        _channel = channel;
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _consumerTask = Task.Run(async () =>
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                if (Handler != null)
                {
                    await Handler(message, _cts.Token);
                }
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    protected override async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_consumerTask != null)
        {
            try
            {
                await _consumerTask;
            }
            catch (OperationCanceledException)
            {
                // Esperado
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();

        if (_consumerTask != null)
        {
            try
            {
                await _consumerTask;
            }
            catch { }
        }

        _cts?.Dispose();
    }
}
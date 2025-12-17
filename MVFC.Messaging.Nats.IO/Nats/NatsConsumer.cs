namespace MVFC.Messaging.Nats.IO.Nats;

public sealed class NatsConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly string _subject;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    public NatsConsumer(string url, string subject)
    {
        _subject = subject;
        var opts = new NatsOpts { Url = url };
        _connection = new NatsConnection(opts);
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _consumeTask = Task.Run(async () =>
        {
            await foreach (var msg in _connection.SubscribeAsync<string>(_subject).WithCancellation(_cts.Token))
            {
                try
                {
                    if (!string.IsNullOrEmpty(msg.Data))
                    {
                        var message = JsonSerializer.Deserialize<T>(msg.Data);

                        if (Handler != null && message != null)
                        {
                            await Handler(message, _cts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Log error
                }
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask;
            }
            catch { }
        }

        await _connection.DisposeAsync();
        _cts?.Dispose();
    }
}
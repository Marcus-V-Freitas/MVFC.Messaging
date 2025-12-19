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
        _connection = CreateNatsConnection(url);
    }

    private static NatsConnection CreateNatsConnection(string url)
    {
        var opts = new NatsOpts { Url = url };
        return new NatsConnection(opts);
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ExecuteConsumeLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ExecuteConsumeLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var natsMessage in SubscribeToSubjectAsync(cancellationToken))
            {
                await ProcessNatsMessageAsync(natsMessage, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private ConfiguredCancelableAsyncEnumerable<NatsMsg<string>> SubscribeToSubjectAsync(CancellationToken cancellationToken)
    {
        return _connection
            .SubscribeAsync<string>(_subject, cancellationToken: cancellationToken)
            .WithCancellation(cancellationToken);
    }

    private async Task ProcessNatsMessageAsync(NatsMsg<string> natsMessage, CancellationToken cancellationToken)
    {
        try
        {
            if (IsValidMessageData(natsMessage.Data))
            {
                var message = DeserializeMessage(natsMessage.Data!);

                if (ShouldInvokeHandler(message))
                {
                    await Handler!(message!, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Log or handle processing errors as needed.
        }
    }

    private static bool IsValidMessageData(string? data)
    {
        return !string.IsNullOrEmpty(data);
    }

    private static T? DeserializeMessage(string messageData)
    {
        return JsonSerializer.Deserialize<T>(messageData);
    }

    private bool ShouldInvokeHandler(T? message)
    {
        return Handler is not null && message is not null;
    }

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

        await _connection.DisposeAsync();
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
            // Ignore exceptions during disposal.
        }
    }
}
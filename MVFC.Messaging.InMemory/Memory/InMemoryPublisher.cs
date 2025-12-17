namespace MVFC.Messaging.InMemory.Memory;

public sealed class InMemoryPublisher<T>(Channel<T> channel) : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly Channel<T> _channel = channel;

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await _channel.Writer.WriteAsync(message, cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
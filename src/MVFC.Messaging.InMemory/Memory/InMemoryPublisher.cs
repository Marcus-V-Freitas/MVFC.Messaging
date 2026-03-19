namespace MVFC.Messaging.InMemory.Memory;

public sealed class InMemoryPublisher<T>(Channel<T> channel) : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly Channel<T> _channel = channel;

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken) =>
        await _channel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        foreach (var message in messages)
        {
            await _channel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() =>
        await ValueTask.CompletedTask.ConfigureAwait(false);
}

namespace MVFC.Messaging.Core.Base;

public abstract class MessagePublisherBase<T> : IMessagePublisher<T>
{
    protected abstract Task PublishInternalAsync(T message, CancellationToken cancellationToken);
    protected abstract Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken);

    public async Task PublishAsync(T message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await PublishInternalAsync(message, cancellationToken);
    }

    public async Task PublishBatchAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        await PublishBatchInternalAsync(messages, cancellationToken);
    }
}
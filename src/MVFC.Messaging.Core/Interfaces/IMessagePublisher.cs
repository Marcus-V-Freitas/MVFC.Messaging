namespace MVFC.Messaging.Core.Interfaces;

public interface IMessagePublisher<in T>
{
    public Task PublishAsync(T message, CancellationToken cancellationToken = default);

    public Task PublishBatchAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default);
}

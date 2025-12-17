namespace MVFC.Messaging.Core.Interfaces;

public interface IMessagePublisher<T>
{
    Task PublishAsync(T message, CancellationToken cancellationToken = default);
    Task PublishBatchAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default);
}
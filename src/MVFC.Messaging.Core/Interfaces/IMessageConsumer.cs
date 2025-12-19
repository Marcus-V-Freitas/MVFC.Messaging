namespace MVFC.Messaging.Core.Interfaces;

public interface IMessageConsumer<T>
{
    Task StartAsync(Func<T, CancellationToken, Task> messageHandler, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
namespace MVFC.Messaging.Core.Interfaces;

public interface IMessageConsumer<T>
{
    public Task StartAsync(Func<T, CancellationToken, Task> messageHandler, CancellationToken cancellationToken = default);

    public Task StopAsync(CancellationToken cancellationToken = default);
}

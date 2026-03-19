namespace MVFC.Messaging.Core.Base;

public abstract class MessageConsumerBase<T> : IMessageConsumer<T>
{
    protected Func<T, CancellationToken, Task>? Handler { get; private set; }

    protected abstract Task StartInternalAsync(CancellationToken cancellationToken);

    protected abstract Task StopInternalAsync(CancellationToken cancellationToken);

    public async Task StartAsync(Func<T, CancellationToken, Task> messageHandler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageHandler);

        Handler = messageHandler;
        await StartInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default) =>
        await StopInternalAsync(cancellationToken).ConfigureAwait(false);
}

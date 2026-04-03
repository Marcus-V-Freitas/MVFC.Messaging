namespace MVFC.Messaging.Tests.Helpers;

public abstract class FixtureBaseTest<T> : IAsyncLifetime
    where T : IContainer
{
    protected T Container { get; set; } = default!;

    public abstract string ConnectionString();

    public virtual async ValueTask InitializeAsync() =>
        await Container.StartAsync().ConfigureAwait(false);

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Container.DisposeAsync().ConfigureAwait(false);
    }
}

namespace MVFC.Messaging.Tests.Helpers;

public abstract class FixtureBaseTest<T> : IAsyncLifetime
    where T : IContainer
{
    protected T _container = default!;

    public abstract string ConnectionString();

    public virtual async ValueTask InitializeAsync() =>
        await _container.StartAsync().ConfigureAwait(false);

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _container.DisposeAsync().ConfigureAwait(false);
    }        
}

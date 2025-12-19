namespace MVFC.Messaging.Tests.Helpers;

public abstract class FixtureBaseTest<T> : IAsyncLifetime
    where T : IContainer
{
    protected T Container = default!;

    public abstract string ConnectionString();

    public virtual async Task InitializeAsync() =>
        await Container.StartAsync();

    public virtual async Task DisposeAsync() =>
        await Container.DisposeAsync();
}
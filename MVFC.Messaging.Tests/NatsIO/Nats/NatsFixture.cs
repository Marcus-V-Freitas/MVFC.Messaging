namespace MVFC.Messaging.Tests.NatsIO.Nats;

public sealed class NatsFixture : IAsyncLifetime
{
    private NatsContainer? _container;

    public string Url => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async Task InitializeAsync()
    {
        _container = new NatsBuilder()
            .WithImage("nats:2.10-alpine")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}
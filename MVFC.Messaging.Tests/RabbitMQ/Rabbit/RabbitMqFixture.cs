namespace MVFC.Messaging.Tests.RabbitMQ.Rabbit;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async Task InitializeAsync()
    {
        _container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}
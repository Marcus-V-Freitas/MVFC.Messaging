namespace MVFC.Messaging.Tests.Confluent.Kafka;

public sealed class KafkaFixture : IAsyncLifetime
{
    private KafkaContainer? _container;

    public string BootstrapServers => _container?.GetBootstrapAddress()
        ?? throw new InvalidOperationException("Container not initialized");

    public async Task InitializeAsync()
    {
        _container = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.0")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}
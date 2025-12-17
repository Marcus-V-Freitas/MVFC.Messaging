namespace MVFC.Messaging.Tests.AWS.SQS;

public sealed class LocalStackFixture : IAsyncLifetime
{
    private LocalStackContainer? _container;

    public string ServiceUrl => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async Task InitializeAsync()
    {
        _container = new LocalStackBuilder()
            .WithImage("localstack/localstack:latest")
            .WithEnvironment("SERVICES", "sqs")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}
namespace MVFC.Messaging.Tests.GCP.PubSub;

public sealed class PubSubFixture : IAsyncLifetime
{
    private readonly PubSubContainer _container = new PubSubBuilder()
                                                         .WithImage("gcr.io/google.com/cloudsdktool/cloud-sdk:emulators")
                                                         .Build();

    public string EmulatorHost => _container?.GetEmulatorEndpoint()
        ?? throw new InvalidOperationException("Container not initialized");

    public int Port => _container?.GetMappedPublicPort(8085) ?? 0;

    public async Task InitializeAsync() =>
        await _container.StartAsync();

    public async Task DisposeAsync() =>
        await _container.DisposeAsync();
}
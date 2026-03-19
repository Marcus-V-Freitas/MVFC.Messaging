namespace MVFC.Messaging.Tests.TestProviders.Azure.ServiceBus;

public sealed class ServiceBusFixture : FixtureBaseTest<ServiceBusContainer>
{
    private readonly INetwork _network;
    private readonly MsSqlContainer _sqlContainer;

    public ServiceBusFixture()
    {
        _network = new NetworkBuilder()
                           .WithName("sb-emulator")
                           .Build();

        _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/azure-sql-edge:latest")
                                .WithPassword("Strong!Passw0rd")
                                .WithNetwork(_network)
                                .WithNetworkAliases("sql-edge")
                                .Build();

        _container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
                               .WithMsSqlContainer(_network, _sqlContainer, "sql-edge", "Strong!Passw0rd")
                               .WithConfig(Path.Combine(Directory.GetCurrentDirectory(), "TestProviders", "Azure", "ServiceBus", "dados.json"))
                               .WithAcceptLicenseAgreement(true)
                               .Build();
    }

    public override async ValueTask InitializeAsync()
    {
        await _network.CreateAsync().ConfigureAwait(false);
        await _sqlContainer.StartAsync().ConfigureAwait(false);
        await _container.StartAsync().ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
        await _sqlContainer.DisposeAsync().ConfigureAwait(false);
        await _network.DeleteAsync().ConfigureAwait(false);
    }

    public override string ConnectionString() =>
        _container.GetConnectionString();
}

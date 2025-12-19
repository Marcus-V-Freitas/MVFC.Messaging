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

        _sqlContainer = new MsSqlBuilder()
                                .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
                                .WithPassword("Strong!Passw0rd")
                                .WithNetwork(_network)
                                .WithNetworkAliases("sql-edge")
                                .Build();

        Container = new ServiceBusBuilder()
                               .WithMsSqlContainer(_network, _sqlContainer, "sql-edge", "Strong!Passw0rd")
                               .WithConfig(Path.Combine(Directory.GetCurrentDirectory(), "TestProviders", "Azure", "ServiceBus", "dados.json"))
                               .WithAcceptLicenseAgreement(true)
                               .Build();
    }

    public override async Task InitializeAsync()
    {
        await _network.CreateAsync();
        await _sqlContainer.StartAsync();
        await Container.StartAsync();
    }

    public override async Task DisposeAsync()
    {
        await Container.DisposeAsync();
        await _sqlContainer.DisposeAsync();
        await _network.DeleteAsync();
    }

    public override string ConnectionString() =>
        Container.GetConnectionString();
}
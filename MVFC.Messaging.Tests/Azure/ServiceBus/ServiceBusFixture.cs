namespace MVFC.Messaging.Tests.Azure.ServiceBus;

public sealed class ServiceBusFixture : IAsyncLifetime
{
    private INetwork? _network;
    private MsSqlContainer? _sqlContainer;
    private IContainer? _serviceBusContainer;

    public const string ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public async Task InitializeAsync()
    {
        _network = new NetworkBuilder()
            .WithName("sb-emulator")
            .Build();

        await _network.CreateAsync();

        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
            .WithPassword("Strong!Passw0rd")
            .WithNetwork(_network)
            .WithNetworkAliases("sql-edge")
            .WithEnvironment("ACCEPT_EULA", "1")
            .WithEnvironment("MSSQL_SA_PASSWORD", "Strong!Passw0rd")
            .Build();

        await _sqlContainer.StartAsync();

        var configDir = Path.Combine(Path.GetTempPath(), $"sb-config-{Guid.NewGuid()}");
        Directory.CreateDirectory(configDir);

        var configContent = @"{
  ""UserConfig"": {
    ""Namespaces"": [
      {
        ""Name"": ""sbemulatorns"",
        ""Queues"": [
          {
            ""Name"": ""queue.1""
          }
        ],
        ""Topics"": []
      }
    ],
    ""Logging"": {
      ""Type"": ""Console""
    }
  }
}";

        await File.WriteAllTextAsync(Path.Combine(configDir, "Config.json"), configContent);

        _serviceBusContainer = new ContainerBuilder()
            .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithNetwork(_network)
            .WithPortBinding(5672, 5672)
            .WithBindMount(configDir, "/ServiceBus_Emulator/ConfigFiles")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SQL_SERVER", "sql-edge")
            .WithEnvironment("MSSQL_SA_PASSWORD", "Strong!Passw0rd")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Emulator Service is Successfully Up!"))
            .Build();

        await _serviceBusContainer.StartAsync();

        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        if (_serviceBusContainer != null)
            await _serviceBusContainer.DisposeAsync();

        if (_sqlContainer != null)
            await _sqlContainer.DisposeAsync();

        if (_network != null)
            await _network.DeleteAsync();
    }
}

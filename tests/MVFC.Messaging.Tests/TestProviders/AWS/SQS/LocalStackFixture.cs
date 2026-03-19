namespace MVFC.Messaging.Tests.TestProviders.AWS.SQS;

public sealed class LocalStackFixture : FixtureBaseTest<LocalStackContainer>
{
    public LocalStackFixture()
    {
        _container = new LocalStackBuilder("localstack/localstack:latest")
                              .WithEnvironment("SERVICES", "sqs")
                              .Build();
    }


    public override string ConnectionString() => 
        _container.GetConnectionString();
}

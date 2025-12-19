namespace MVFC.Messaging.Tests.TestProviders.AWS.SQS;

public sealed class LocalStackFixture : FixtureBaseTest<LocalStackContainer>
{
    public LocalStackFixture()
    {
        Container = new LocalStackBuilder()
                              .WithImage("localstack/localstack:latest")
                              .WithEnvironment("SERVICES", "sqs")
                              .Build();
    }


    public override string ConnectionString() => 
        Container.GetConnectionString();
}
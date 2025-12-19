namespace MVFC.Messaging.Tests.TestProviders.NatsIO.Nats;

public sealed class NatsFixture : FixtureBaseTest<NatsContainer>
{
    public NatsFixture()
    {
        Container = new NatsBuilder()
                          .WithImage("nats:2.10-alpine")
                          .Build();
    }

    public override string ConnectionString() =>
        Container.GetConnectionString();
}
namespace MVFC.Messaging.Tests.TestProviders.NatsIO.Nats;

public sealed class NatsFixture : FixtureBaseTest<NatsContainer>
{
    public NatsFixture()
    {
        _container = new NatsBuilder("nats:2.10-alpine")
                          .Build();
    }

    public override string ConnectionString() =>
        _container.GetConnectionString();
}

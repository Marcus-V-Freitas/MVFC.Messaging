namespace MVFC.Messaging.Tests.TestProviders.RabbitMQ.Rabbit;

public sealed class RabbitMqFixture : FixtureBaseTest<RabbitMqContainer>
{
    public RabbitMqFixture()
    {
        Container = new RabbitMqBuilder("rabbitmq:3-management-alpine")
                              .Build();
    }

    public override string ConnectionString() =>
        Container.GetConnectionString();
}

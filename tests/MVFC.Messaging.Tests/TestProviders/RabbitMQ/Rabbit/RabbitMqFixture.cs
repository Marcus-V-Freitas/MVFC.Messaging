namespace MVFC.Messaging.Tests.TestProviders.RabbitMQ.Rabbit;

public sealed class RabbitMqFixture : FixtureBaseTest<RabbitMqContainer>
{
    public RabbitMqFixture()
    {
        _container = new RabbitMqBuilder("rabbitmq:3-management-alpine")
                              .Build();
    }

    public override string ConnectionString() =>
        _container.GetConnectionString();
}

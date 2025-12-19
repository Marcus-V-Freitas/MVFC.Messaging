namespace MVFC.Messaging.Tests.TestProviders.Confluent.Kafka;

public sealed class KafkaFixture : FixtureBaseTest<KafkaContainer>
{
    public KafkaFixture()
    {
        Container = new KafkaBuilder().Build();
    }

    public override string ConnectionString() =>
        Container.GetBootstrapAddress();
}
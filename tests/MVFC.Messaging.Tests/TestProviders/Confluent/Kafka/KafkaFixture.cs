namespace MVFC.Messaging.Tests.TestProviders.Confluent.Kafka;

public sealed class KafkaFixture : FixtureBaseTest<KafkaContainer>
{
    public KafkaFixture()
    {
        Container = new KafkaBuilder("confluentinc/cp-kafka:7.5.12").Build();
    }

    public override string ConnectionString() =>
        Container.GetBootstrapAddress();
}

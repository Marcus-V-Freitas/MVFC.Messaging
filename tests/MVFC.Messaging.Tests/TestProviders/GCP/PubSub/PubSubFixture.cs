namespace MVFC.Messaging.Tests.TestProviders.GCP.PubSub;

public sealed class PubSubFixture : FixtureBaseTest<PubSubContainer>
{
    public PubSubFixture()
    {
        _container = new PubSubBuilder("gcr.io/google.com/cloudsdktool/cloud-sdk:emulators")
                         .Build();
    }

    public override string ConnectionString() =>
        _container.GetEmulatorEndpoint();
}

namespace MVFC.Messaging.Tests.TestProviders.GCP.PubSub;

public sealed class PubSubFixture : FixtureBaseTest<PubSubContainer>
{
    public PubSubFixture()
    {
        Container = new PubSubBuilder()
                         .WithImage("gcr.io/google.com/cloudsdktool/cloud-sdk:emulators")
                         .Build();
    }

    public override string ConnectionString() =>
        Container.GetEmulatorEndpoint();
}
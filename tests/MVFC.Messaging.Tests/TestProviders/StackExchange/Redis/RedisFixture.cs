namespace MVFC.Messaging.Tests.TestProviders.StackExchange.Redis;

public sealed class RedisFixture : FixtureBaseTest<RedisContainer>
{
    public RedisFixture()
    {
        Container = new RedisBuilder()
                             .WithImage("redis:7-alpine")
                             .Build();
    }

    public override string ConnectionString() =>
        Container.GetConnectionString();
}
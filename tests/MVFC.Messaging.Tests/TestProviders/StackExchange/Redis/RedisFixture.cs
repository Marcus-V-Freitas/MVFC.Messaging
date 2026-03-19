namespace MVFC.Messaging.Tests.TestProviders.StackExchange.Redis;

public sealed class RedisFixture : FixtureBaseTest<RedisContainer>
{
    public RedisFixture()
    {
        _container = new RedisBuilder("redis:7-alpine")
                             .Build();
    }

    public override string ConnectionString() =>
        _container.GetConnectionString();
}

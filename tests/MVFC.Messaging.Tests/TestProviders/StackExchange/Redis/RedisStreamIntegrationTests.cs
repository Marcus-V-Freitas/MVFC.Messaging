namespace MVFC.Messaging.Tests.TestProviders.StackExchange.Redis;

public sealed class RedisStreamIntegrationTests(RedisFixture fixture, ITestOutputHelper output) : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string streamKey = "test-stream";
        const string consumerGroup = "test-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, streamKey);
        await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, streamKey, consumerGroup);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(1000);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "Redis Stream Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        receivedMessage.Should().NotBeNull();
        sentMessage.Id.Should().Be(receivedMessage.Id);
        sentMessage.Content.Should().Be(receivedMessage.Content);

        await consumer.StopAsync();
    }

    [Fact]
    public async Task Should_PublishAndConsume_BatchMessages()
    {
        // Arrange
        const string streamKey = "test-batch-stream";
        const string consumerGroup = "test-batch-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, streamKey);
        await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, streamKey, consumerGroup);

        var receivedMessages = new List<TestMessage>();
        var tcs = new TaskCompletionSource<bool>();

        await consumer.StartAsync(async (msg, ct) =>
        {
            lock (receivedMessages)
            {
                receivedMessages.Add(msg);
                _output.WriteLine($"Received {receivedMessages.Count}: {msg.Content}");
                if (receivedMessages.Count == 3)
                    tcs.SetResult(true);
            }
        }, CancellationToken.None);

        await Task.Delay(1000);

        // Act
        var messages = new[]
        {
            new TestMessage { Id = 1, Content = "Redis Batch 1" },
            new TestMessage { Id = 2, Content = "Redis Batch 2" },
            new TestMessage { Id = 3, Content = "Redis Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync();
    }
}
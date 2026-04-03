namespace MVFC.Messaging.Tests.TestProviders.StackExchange.Redis;

public sealed class RedisStreamIntegrationTests(RedisFixture fixture, ITestOutputHelper output) : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string STREAM_KEY = "test-stream";
        const string CONSUMER_GROUP = "test-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, STREAM_KEY);
        await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, STREAM_KEY, CONSUMER_GROUP);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "Redis Stream Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        receivedMessage.Should().NotBeNull();
        sentMessage.Id.Should().Be(receivedMessage.Id);
        sentMessage.Content.Should().Be(receivedMessage.Content);

        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_PublishAndConsume_BatchMessages()
    {
        // Arrange
        const string STREAM_KEY = "test-batch-stream";
        const string CONSUMER_GROUP = "test-batch-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, STREAM_KEY);
        await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, STREAM_KEY, CONSUMER_GROUP);

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

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        var messages = new[]
        {
            new TestMessage { Id = 1, Content = "Redis Batch 1" },
            new TestMessage { Id = 2, Content = "Redis Batch 2" },
            new TestMessage { Id = 3, Content = "Redis Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_DisposeAsync_Correctly()
    {
        // Arrange
        var connectionString = _fixture.ConnectionString();
        var consumer = new RedisStreamConsumer<TestMessage>(connectionString, "test-stream", "test-group");
        await consumer.StartAsync((msg, ct) => Task.CompletedTask, CancellationToken.None);

        // Act & Assert
        await consumer.DisposeAsync();
        // No exceptions should be thrown
    }

    [Fact]
    public async Task Should_HandleException_InHandler()
    {
        // Arrange
        const string STREAM_KEY = "test-exception-stream";
        const string CONSUMER_GROUP = "test-exception-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, STREAM_KEY);
        await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, STREAM_KEY, CONSUMER_GROUP);

        var tcs = new TaskCompletionSource<bool>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            tcs.SetResult(true);
            throw new InvalidOperationException("Test Exception");
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        await publisher.PublishAsync(new TestMessage { Id = 1, Content = "Exception Test" }, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        // The consumer should handle the exception and continue
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Should_StartAsync_WhenConsumerGroupAlreadyExists()
    {
        // Arrange
        const string STREAM_KEY = "test-duplicate-group-stream";
        const string CONSUMER_GROUP = "test-duplicate-group";
        var connectionString = _fixture.ConnectionString();

        var consumer1 = new RedisStreamConsumer<TestMessage>(connectionString, STREAM_KEY, CONSUMER_GROUP);
        await consumer1.StartAsync(async (msg, ct) => await Task.CompletedTask.ConfigureAwait(true), CancellationToken.None);

        var consumer2 = new RedisStreamConsumer<TestMessage>(connectionString, STREAM_KEY, CONSUMER_GROUP);

        // Act
        var act = async () => await consumer2.StartAsync(async (msg, ct) => await Task.CompletedTask.ConfigureAwait(true), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        await consumer1.StopAsync(CancellationToken.None);
        await consumer2.StopAsync(CancellationToken.None);
        await consumer1.DisposeAsync();
        await consumer2.DisposeAsync();
    }
    [Fact]
    public async Task Should_HandleNullMessage_InShouldInvokeHandler()
    {
        // Arrange
        const string STREAM_KEY = "test-null-stream";
        const string CONSUMER_GROUP = "test-null-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new RedisStreamPublisher<string>(connectionString, STREAM_KEY);
        await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, STREAM_KEY, CONSUMER_GROUP);

        var handlerInvoked = false;
        await consumer.StartAsync((msg, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        // Publish JSON "null"
        await publisher.PublishAsync("null", CancellationToken.None);
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Assert
        handlerInvoked.Should().BeFalse();
        await consumer.StopAsync(CancellationToken.None);
    }
}

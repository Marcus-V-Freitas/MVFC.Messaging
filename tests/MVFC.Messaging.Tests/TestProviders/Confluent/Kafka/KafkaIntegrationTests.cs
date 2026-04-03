namespace MVFC.Messaging.Tests.TestProviders.Confluent.Kafka;

public sealed class KafkaIntegrationTests(KafkaFixture fixture, ITestOutputHelper output) : IClassFixture<KafkaFixture>
{
    private readonly KafkaFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string TOPIC_ID = "test-topic";
        const string GROUP_ID = "test-group";
        var connectioString = _fixture.ConnectionString();

        await using var publisher = new KafkaPublisher<TestMessage>(connectioString, TOPIC_ID);
        await using var consumer = new KafkaConsumer<TestMessage>(connectioString, TOPIC_ID, GROUP_ID);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "Kafka Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

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
        const string TOPIC_ID = "test-batch-topic";
        const string GROUP_ID = "test-batch-group";
        var connectioString = _fixture.ConnectionString();

        await using var publisher = new KafkaPublisher<TestMessage>(connectioString, TOPIC_ID);
        await using var consumer = new KafkaConsumer<TestMessage>(connectioString, TOPIC_ID, GROUP_ID);

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

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // Act
        var messages = new[]
        {
            new TestMessage { Id = 1, Content = "Kafka Batch 1" },
            new TestMessage { Id = 2, Content = "Kafka Batch 2" },
            new TestMessage { Id = 3, Content = "Kafka Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_DisposeAsync_Correctly()
    {
        // Arrange
        var connectionString = _fixture.ConnectionString();
        var consumer = new KafkaConsumer<TestMessage>(connectionString, "test-topic", "test-group");
        await consumer.StartAsync((msg, ct) => Task.CompletedTask, CancellationToken.None);

        // Act & Assert
        await consumer.DisposeAsync();
        // No exceptions should be thrown
    }

    [Fact]
    public async Task Should_HandleException_InHandler()
    {
        // Arrange
        const string TOPIC_ID = "test-exception-topic";
        const string GROUP_ID = "test-exception-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new KafkaPublisher<TestMessage>(connectionString, TOPIC_ID);
        await using var consumer = new KafkaConsumer<TestMessage>(connectionString, TOPIC_ID, GROUP_ID);

        var tcs = new TaskCompletionSource<bool>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            tcs.SetResult(true);
            throw new InvalidOperationException("Test Exception");
        }, CancellationToken.None);

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // Act
        await publisher.PublishAsync(new TestMessage { Id = 1, Content = "Exception Test" }, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        // The consumer should handle the exception and continue (implicitly tested by stopping it)
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Should_HandleNullMessage_InShouldInvokeHandler()
    {
        // Arrange
        const string TOPIC_ID = "test-null-topic";
        const string GROUP_ID = "test-null-group";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new KafkaPublisher<string>(connectionString, TOPIC_ID);
        await using var consumer = new KafkaConsumer<TestMessage>(connectionString, TOPIC_ID, GROUP_ID);

        var handlerInvoked = false;
        await consumer.StartAsync((msg, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // Act
        // Publish JSON "null" which deserializes to null for reference types
        await publisher.PublishAsync("null", CancellationToken.None);
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Assert
        handlerInvoked.Should().BeFalse();
        await consumer.StopAsync(CancellationToken.None);
    }
}

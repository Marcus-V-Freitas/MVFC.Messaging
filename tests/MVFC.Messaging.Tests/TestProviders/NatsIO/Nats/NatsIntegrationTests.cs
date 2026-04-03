namespace MVFC.Messaging.Tests.TestProviders.NatsIO.Nats;

public sealed class NatsIntegrationTests(NatsFixture fixture, ITestOutputHelper output) : IClassFixture<NatsFixture>
{
    private readonly NatsFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string SUBJECT = "test.subject";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new NatsPublisher<TestMessage>(connectionString, SUBJECT);
        await using var consumer = new NatsConsumer<TestMessage>(connectionString, SUBJECT);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "NATS Test" };
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
        const string SUBJECT = "test.batch.subject";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new NatsPublisher<TestMessage>(connectionString, SUBJECT);
        await using var consumer = new NatsConsumer<TestMessage>(connectionString, SUBJECT);

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
            new TestMessage { Id = 1, Content = "NATS Batch 1" },
            new TestMessage { Id = 2, Content = "NATS Batch 2" },
            new TestMessage { Id = 3, Content = "NATS Batch 3" }
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
        var consumer = new NatsConsumer<TestMessage>(connectionString, "test.dispose");
        await consumer.StartAsync((msg, ct) => Task.CompletedTask, CancellationToken.None);

        // Act & Assert
        await consumer.DisposeAsync();
        // No exceptions should be thrown
    }

    [Fact]
    public async Task Should_HandleException_InHandler()
    {
        // Arrange
        const string SUBJECT = "test.exception.subject";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new NatsPublisher<TestMessage>(connectionString, SUBJECT);
        await using var consumer = new NatsConsumer<TestMessage>(connectionString, SUBJECT);

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
        // The consumer should not crash and continue processing (though we only test one message here)
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Should_HandleNullMessage_InShouldInvokeHandler()
    {
        // Arrange
        const string SUBJECT = "test.null.subject";
        var connectionString = _fixture.ConnectionString();
        await using var publisher = new NatsPublisher<string>(connectionString, SUBJECT);
        await using var consumer = new NatsConsumer<TestMessage>(connectionString, SUBJECT);

        var handlerInvoked = false;
        await consumer.StartAsync((msg, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        // Publish something that is NOT a valid TestMessage JSON (e.g. empty string or invalid JSON)
        // But IsValidMessageData checks for null or empty.
        // If I publish " ", it passes IsValidMessageData but DeserializeMessage returns null.
        await publisher.PublishAsync(" ", CancellationToken.None);
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // Assert
        handlerInvoked.Should().BeFalse();
        await consumer.StopAsync(CancellationToken.None);
    }
}

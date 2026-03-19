namespace MVFC.Messaging.Tests.TestProviders.Azure.ServiceBus;

public sealed class ServiceBusIntegrationTests(ServiceBusFixture fixture, ITestOutputHelper output) : IClassFixture<ServiceBusFixture>
{
    private readonly ServiceBusFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string QUEUE_NAME = "queue.single";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, QUEUE_NAME);
        await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, QUEUE_NAME);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        var sentMessage = new TestMessage { Id = 1, Content = "ServiceBus Test" };
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
        const string QUEUE_NAME = "queue.batch";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, QUEUE_NAME);
        await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, QUEUE_NAME);

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
            new TestMessage { Id = 1, Content = "Batch 1" },
            new TestMessage { Id = 2, Content = "Batch 2" },
            new TestMessage { Id = 3, Content = "Batch 3" }
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
        const string QUEUE_NAME = "dispose.queue";
        var connectionString = _fixture.ConnectionString();
        var publisher = new ServiceBusPublisher<TestMessage>(connectionString, QUEUE_NAME);
        var consumer = new ServiceBusConsumer<TestMessage>(connectionString, QUEUE_NAME);

        // Act
        await publisher.DisposeAsync();
        await consumer.DisposeAsync();

        // Assert
        // No exceptions should be thrown
    }

    [Fact]
    public async Task Should_HandleException_InHandler()
    {
        // Arrange
        const string QUEUE_NAME = "queue.exception";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, QUEUE_NAME);
        await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, QUEUE_NAME);

        var tcs = new TaskCompletionSource<bool>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            tcs.TrySetResult(true);
            throw new Exception("Test Exception");
        }, CancellationToken.None);

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // Act
        await publisher.PublishAsync(new TestMessage { Id = 1, Content = "Exception Test" }, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        // The consumer should handle the exception and move forward
        await consumer.StopAsync(CancellationToken.None);
    }
    [Fact]
    public async Task Should_TriggerBatchFull_InPublisher()
    {
        // Arrange
        const string QUEUE_NAME = "queue.batch.full";
        var connectionString = _fixture.ConnectionString();
        await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, QUEUE_NAME);

        // Act
        // Create 1000 messages with large content to force multiple batches
        var messages = Enumerable.Range(1, 1000)
            .Select(i => new TestMessage { Id = i, Content = new string('A', 5000) })
            .ToArray();

        // Act & Assert
        var act = async () => await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Message could not be added to batch. Batch size limit reached.");
    }
}

namespace MVFC.Messaging.Tests.TestProviders.RabbitMQ.Rabbit;

public sealed class RabbitMqIntegrationTests(RabbitMqFixture fixture, ITestOutputHelper output) : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string QUEUE_NAME = "test-rabbit-queue";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = await RabbitMqPublisher.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);
        await using var consumer = await RabbitMqConsumer.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "RabbitMQ Test" };
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
        const string QUEUE_NAME = "test-rabbit-batch";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = await RabbitMqPublisher.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);
        await using var consumer = await RabbitMqConsumer.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);

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
            new TestMessage { Id = 1, Content = "Batch 1" },
            new TestMessage { Id = 2, Content = "Batch 2" },
            new TestMessage { Id = 3, Content = "Batch 3" }
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
        const string QUEUE_NAME = "dispose.rabbit.queue";
        var connectionString = _fixture.ConnectionString();
        var publisher = await RabbitMqPublisher.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);
        var consumer = await RabbitMqConsumer.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);

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
        const string QUEUE_NAME = "exception.rabbit.queue";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = await RabbitMqPublisher.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);
        await using var consumer = await RabbitMqConsumer.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);

        var tcs = new TaskCompletionSource<bool>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            tcs.TrySetResult(true);
            throw new InvalidOperationException("Test Exception");
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        await publisher.PublishAsync(new TestMessage { Id = 1, Content = "Exception Test" }, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        // The consumer should handle the exception (Nack/Reject) and we should be able to stop it
        await consumer.StopAsync(CancellationToken.None);
    }
    [Fact]
    public async Task Should_HandleNullMessage_InShouldInvokeHandler()
    {
        // Arrange
        const string QUEUE_NAME = "null.rabbit.queue";
        var connectionString = _fixture.ConnectionString();
        await using var publisher = await RabbitMqPublisher.CreateAsync<string>(connectionString, QUEUE_NAME);
        await using var consumer = await RabbitMqConsumer.CreateAsync<TestMessage>(connectionString, QUEUE_NAME);

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

namespace MVFC.Messaging.Tests.RabbitMQ.Rabbit;

public sealed class RabbitMqIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RabbitMqIntegrationTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string queueName = "test-rabbit-queue";

        await using var publisher = new RabbitMqPublisher<TestMessage>(_fixture.ConnectionString, queueName);
        await using var consumer = new RabbitMqConsumer<TestMessage>(_fixture.ConnectionString, queueName);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(1000);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "RabbitMQ Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(sentMessage.Id, receivedMessage.Id);
        Assert.Equal(sentMessage.Content, receivedMessage.Content);

        await consumer.StopAsync();
    }

    [Fact]
    public async Task Should_PublishAndConsume_BatchMessages()
    {
        // Arrange
        const string queueName = "test-rabbit-batch";

        await using var publisher = new RabbitMqPublisher<TestMessage>(_fixture.ConnectionString, queueName);
        await using var consumer = new RabbitMqConsumer<TestMessage>(_fixture.ConnectionString, queueName);

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
            new TestMessage { Id = 1, Content = "Batch 1" },
            new TestMessage { Id = 2, Content = "Batch 2" },
            new TestMessage { Id = 3, Content = "Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(3, receivedMessages.Count);

        await consumer.StopAsync();
    }
}
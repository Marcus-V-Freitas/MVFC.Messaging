namespace MVFC.Messaging.Tests.TestProviders.Azure.ServiceBus;

public sealed class ServiceBusIntegrationTests(ServiceBusFixture fixture, ITestOutputHelper output) : IClassFixture<ServiceBusFixture>
{
    private readonly ServiceBusFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string queueName = "queue.1";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, queueName);
        await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, queueName);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(2000);

        var sentMessage = new TestMessage { Id = 1, Content = "ServiceBus Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

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
        const string queueName = "queue.1";
        var connectionString = _fixture.ConnectionString();

        await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, queueName);
        await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, queueName);

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

        await Task.Delay(2000);

        // Act
        var messages = new[]
        {
            new TestMessage { Id = 1, Content = "Batch 1" },
            new TestMessage { Id = 2, Content = "Batch 2" },
            new TestMessage { Id = 3, Content = "Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync();
    }
}
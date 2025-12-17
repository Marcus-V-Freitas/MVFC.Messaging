namespace MVFC.Messaging.Tests.NatsIO.Nats;

public sealed class NatsIntegrationTests : IClassFixture<NatsFixture>
{
    private readonly NatsFixture _fixture;
    private readonly ITestOutputHelper _output;

    public NatsIntegrationTests(NatsFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string subject = "test.subject";

        await using var publisher = new NatsPublisher<TestMessage>(_fixture.Url, subject);
        await using var consumer = new NatsConsumer<TestMessage>(_fixture.Url, subject);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(1000);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "NATS Test" };
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
        const string subject = "test.batch.subject";

        await using var publisher = new NatsPublisher<TestMessage>(_fixture.Url, subject);
        await using var consumer = new NatsConsumer<TestMessage>(_fixture.Url, subject);

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
            new TestMessage { Id = 1, Content = "NATS Batch 1" },
            new TestMessage { Id = 2, Content = "NATS Batch 2" },
            new TestMessage { Id = 3, Content = "NATS Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(3, receivedMessages.Count);

        await consumer.StopAsync();
    }
}
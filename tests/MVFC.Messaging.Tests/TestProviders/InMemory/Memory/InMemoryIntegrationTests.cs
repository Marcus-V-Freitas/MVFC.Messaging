namespace MVFC.Messaging.Tests.TestProviders.InMemory.Memory;

public sealed class InMemoryIntegrationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<TestMessage>();
        var publisher = new InMemoryPublisher<TestMessage>(channel);
        var consumer = new InMemoryConsumer<TestMessage>(channel);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "Memory Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
        var channel = Channel.CreateUnbounded<TestMessage>();
        var publisher = new InMemoryPublisher<TestMessage>(channel);
        var consumer = new InMemoryConsumer<TestMessage>(channel);

        var receivedMessages = new List<TestMessage>();
        var tcs = new TaskCompletionSource<bool>();

        await consumer.StartAsync(async (msg, ct) =>
        {
            lock (receivedMessages)
            {
                receivedMessages.Add(msg);
                if (receivedMessages.Count == 5)
                    tcs.SetResult(true);
            }
        }, CancellationToken.None);

        // Act
        var messages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage { Id = i, Content = $"Message {i}" })
            .ToArray();

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(5, receivedMessages.Count);

        await consumer.StopAsync();
    }
}
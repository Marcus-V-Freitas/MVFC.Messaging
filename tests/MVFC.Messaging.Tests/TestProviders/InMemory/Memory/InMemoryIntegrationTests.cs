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

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

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
                if (receivedMessages.Count == 3)
                    tcs.SetResult(true);
            }
        }, CancellationToken.None);

        // Act
        var messages = Enumerable.Range(1, 3)
            .Select(i => new TestMessage { Id = i, Content = $"Message {i}" })
            .ToArray();

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_DisposeAsync_Correctly()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<TestMessage>();
        var consumer = new InMemoryConsumer<TestMessage>(channel);
        await consumer.StartAsync((msg, ct) => Task.CompletedTask, CancellationToken.None);

        // Act & Assert
        await consumer.DisposeAsync();
        // No exceptions should be thrown
    }

    [Fact]
    public async Task Should_CompleteSuccessfully_WhenChannelIsClosed()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<TestMessage>();
        var consumer = new InMemoryConsumer<TestMessage>(channel);
        await consumer.StartAsync((msg, ct) => Task.CompletedTask, CancellationToken.None);

        // Act
        channel.Writer.Complete();
        await consumer.StopAsync(CancellationToken.None);

        // Assert
        // Line 54 and 78 should be hit
    }

    [Fact]
    public async Task Should_DisposePublisher_Correctly()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<TestMessage>();
        var publisher = new InMemoryPublisher<TestMessage>(channel);

        // Act & Assert
        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task Should_StopAsync_WhenHandlerIsNull()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<TestMessage>();
        var consumer = new InMemoryConsumer<TestMessage>(channel);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => consumer.StartAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_WhenPublishBatchMessagesIsNull()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<TestMessage>();
        var publisher = new InMemoryPublisher<TestMessage>(channel);

        // Act
        var act = async () => await publisher.PublishBatchAsync(null!, CancellationToken.None).ConfigureAwait(true);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

namespace MVFC.Messaging.Tests.TestProviders.GCP.PubSub;

public sealed class PubSubIntegrationTests(PubSubFixture fixture, ITestOutputHelper output) : IClassFixture<PubSubFixture>
{
    private readonly PubSubFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;
    private const string PROJECT_ID = "test-project";
    private const string TOPIC_ID = "test-topic";
    private const string SUBSCRIPTION = "test-subscription";

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        await CreateTopicAndSubscriptionAsync();

        await using var publisher = new PubSubPublisher<TestMessage>(PROJECT_ID, TOPIC_ID);
        await using var consumer = new PubSubConsumer<TestMessage>(PROJECT_ID, SUBSCRIPTION);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received message: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        // Act
        var sentMessage = new TestMessage { Id = 100, Content = "PubSub Test" };
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
        const string BATCH_TOPIC_ID = "batch-topic";
        const string BATCH_SUBSCRIPTION_ID = "batch-subscription";

        await CreateTopicAndSubscriptionAsync(BATCH_TOPIC_ID, BATCH_SUBSCRIPTION_ID);

        var publisher = new PubSubPublisher<TestMessage>(PROJECT_ID, BATCH_TOPIC_ID);
        var consumer = new PubSubConsumer<TestMessage>(PROJECT_ID, BATCH_SUBSCRIPTION_ID);

        var receivedMessages = new List<TestMessage>();
        var tcs = new TaskCompletionSource<bool>();

        await consumer.StartAsync(async (msg, ct) =>
        {
            receivedMessages.Add(msg);
            if (receivedMessages.Count == 3)
                tcs.SetResult(true);
        }, CancellationToken.None);

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
        const string DISPOSE_SUB = "dispose-sub";
        await CreateTopicAndSubscriptionAsync("dispose-topic", DISPOSE_SUB);
        var consumer = new PubSubConsumer<TestMessage>(PROJECT_ID, DISPOSE_SUB);
        await consumer.StartAsync((msg, ct) => Task.CompletedTask, CancellationToken.None);

        // Act & Assert
        await consumer.DisposeAsync();
        // No exceptions should be thrown
    }

    [Fact]
    public async Task Should_HandleException_InHandler()
    {
        // Arrange
        const string EXC_TOPIC_ID = "exception-topic";
        const string EXC_SUBSCRIPTION_ID = "exception-subscription";
        await CreateTopicAndSubscriptionAsync(EXC_TOPIC_ID, EXC_SUBSCRIPTION_ID);

        await using var publisher = new PubSubPublisher<TestMessage>(PROJECT_ID, EXC_TOPIC_ID);
        await using var consumer = new PubSubConsumer<TestMessage>(PROJECT_ID, EXC_SUBSCRIPTION_ID);

        var tcs = new TaskCompletionSource<bool>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            tcs.SetResult(true);
            throw new Exception("Test Exception");
        }, CancellationToken.None);

        // Act
        await publisher.PublishAsync(new TestMessage { Id = 1, Content = "Exception Test" }, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        // The consumer should handle the exception (return Nack) and we should be able to stop it
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Should_HandleNullMessage_InShouldInvokeHandler()
    {
        // Arrange
        const string NULL_TOPIC = "null-topic";
        const string NULL_SUB = "null-subscription";
        await CreateTopicAndSubscriptionAsync(NULL_TOPIC, NULL_SUB);

        await using var publisher = new PubSubPublisher<string>(PROJECT_ID, NULL_TOPIC);
        await using var consumer = new PubSubConsumer<TestMessage>(PROJECT_ID, NULL_SUB);

        var handlerInvoked = false;
        await consumer.StartAsync((msg, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        // Act
        // Publish JSON "null" which deserializes to null for reference types
        await publisher.PublishAsync("null", CancellationToken.None);
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Assert
        handlerInvoked.Should().BeFalse();
        await consumer.StopAsync(CancellationToken.None);
    }

    private async Task CreateTopicAndSubscriptionAsync(
        string topicId = TOPIC_ID,
        string subscriptionId = SUBSCRIPTION)
    {
        Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", _fixture.ConnectionString());

        var publisherService = await new PublisherServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();

        var subscriberService = await new SubscriberServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();

        var topicName = TopicName.FromProjectTopic(PROJECT_ID, topicId);

        try
        {
            await publisherService.CreateTopicAsync(topicName).ConfigureAwait(true);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Topic already exists, ignore
        }

        var subscriptionName = SubscriptionName.FromProjectSubscription(PROJECT_ID, subscriptionId);

        try
        {
            await subscriberService.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 60).ConfigureAwait(true);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Subscription already exists, ignore
        }
    }
}

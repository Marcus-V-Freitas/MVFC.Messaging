namespace MVFC.Messaging.Tests.TestProviders.GCP.PubSub;

public sealed class PubSubIntegrationTests(PubSubFixture fixture, ITestOutputHelper output) : IClassFixture<PubSubFixture>
{
    private readonly PubSubFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;
    private const string ProjectId = "test-project";
    private const string TopicId = "test-topic";
    private const string SubscriptionId = "test-subscription";

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        await CreateTopicAndSubscriptionAsync();

        await using var publisher = new PubSubPublisher<TestMessage>(ProjectId, TopicId);
        await using var consumer = new PubSubConsumer<TestMessage>(ProjectId, SubscriptionId);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received message: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        // Act
        var sentMessage = new TestMessage { Id = 100, Content = "PubSub Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

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
        const string batchTopicId = "batch-topic";
        const string batchSubscriptionId = "batch-subscription";

        await CreateTopicAndSubscriptionAsync(batchTopicId, batchSubscriptionId);

        var publisher = new PubSubPublisher<TestMessage>(ProjectId, batchTopicId);
        var consumer = new PubSubConsumer<TestMessage>(ProjectId, batchSubscriptionId);

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
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync();
    }

    private async Task CreateTopicAndSubscriptionAsync(
        string topicId = TopicId,
        string subscriptionId = SubscriptionId)
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

        var topicName = TopicName.FromProjectTopic(ProjectId, topicId);

        try
        {
            await publisherService.CreateTopicAsync(topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Topic already exists, ignore
        }

        var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

        try
        {
            await subscriberService.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 60);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Subscription already exists, ignore
        }
    }
}
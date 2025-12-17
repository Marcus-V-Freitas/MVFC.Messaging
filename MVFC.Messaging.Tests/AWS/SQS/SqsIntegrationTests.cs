namespace MVFC.Messaging.Tests.AWS.SQS;

public sealed class SqsIntegrationTests : IClassFixture<LocalStackFixture>
{
    private readonly LocalStackFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SqsIntegrationTests(LocalStackFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        var sqsClient = new AmazonSQSClient(new AmazonSQSConfig
        {
            ServiceURL = _fixture.ServiceUrl
        });

        var queueName = $"test-queue-{Guid.NewGuid()}";
        var createQueueResponse = await sqsClient.CreateQueueAsync(queueName);
        var queueUrl = createQueueResponse.QueueUrl;

        await using var publisher = new SqsPublisher<TestMessage>(sqsClient, queueUrl);
        await using var consumer = new SqsConsumer<TestMessage>(sqsClient, queueUrl);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "SQS Test" };
        await publisher.PublishAsync(sentMessage, CancellationToken.None);

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

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
        var sqsClient = new AmazonSQSClient(new AmazonSQSConfig
        {
            ServiceURL = _fixture.ServiceUrl
        });

        var queueName = $"test-batch-{Guid.NewGuid()}";
        var createQueueResponse = await sqsClient.CreateQueueAsync(queueName);
        var queueUrl = createQueueResponse.QueueUrl;

        await using var publisher = new SqsPublisher<TestMessage>(sqsClient, queueUrl);
        await using var consumer = new SqsConsumer<TestMessage>(sqsClient, queueUrl);

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
        Assert.Equal(3, receivedMessages.Count);

        await consumer.StopAsync();
    }
}
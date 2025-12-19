namespace MVFC.Messaging.Tests.TestProviders.AWS.SQS;

public sealed class SqsIntegrationTests(LocalStackFixture fixture, ITestOutputHelper output) : IClassFixture<LocalStackFixture>
{
    private readonly ITestOutputHelper _output = output;
    private readonly AmazonSQSClient _sqsClient = new(new AmazonSQSConfig
    {
        ServiceURL = fixture.ConnectionString()
    });

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        var queueName = $"test-queue-{Guid.NewGuid()}";
        var createQueueResponse = await _sqsClient.CreateQueueAsync(queueName);
        var queueUrl = createQueueResponse.QueueUrl;

        await using var publisher = new SqsPublisher<TestMessage>(_sqsClient, queueUrl);
        await using var consumer = new SqsConsumer<TestMessage>(_sqsClient, queueUrl);

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
        receivedMessage.Should().NotBeNull();
        sentMessage.Id.Should().Be(receivedMessage.Id);
        sentMessage.Content.Should().Be(receivedMessage.Content);

        await consumer.StopAsync();
    }

    [Fact]
    public async Task Should_PublishAndConsume_BatchMessages()
    {
        // Arrange
        var queueName = $"test-batch-{Guid.NewGuid()}";
        var createQueueResponse = await _sqsClient.CreateQueueAsync(queueName);
        var queueUrl = createQueueResponse.QueueUrl;

        await using var publisher = new SqsPublisher<TestMessage>(_sqsClient, queueUrl);
        await using var consumer = new SqsConsumer<TestMessage>(_sqsClient, queueUrl);

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
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync();
    }
}
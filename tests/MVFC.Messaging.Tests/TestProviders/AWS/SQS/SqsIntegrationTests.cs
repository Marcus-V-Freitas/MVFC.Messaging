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
        var createQueueResponse = await _sqsClient.CreateQueueAsync(queueName, TestContext.Current.CancellationToken);
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

        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

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
        var queueName = $"test-batch-{Guid.NewGuid()}";
        var createQueueResponse = await _sqsClient.CreateQueueAsync(queueName, TestContext.Current.CancellationToken);
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
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_DisposeAsync_Correctly()
    {
        // Arrange
        var queueName = $"dispose-queue-{Guid.NewGuid()}";
        var createQueueResponse = await _sqsClient.CreateQueueAsync(queueName, CancellationToken.None);
        var queueUrl = createQueueResponse.QueueUrl;
        var publisher = new SqsPublisher<TestMessage>(_sqsClient, queueUrl);
        var consumer = new SqsConsumer<TestMessage>(_sqsClient, queueUrl);

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
        var queueName = $"exception-queue-{Guid.NewGuid()}";
        var createQueueResponse = await _sqsClient.CreateQueueAsync(queueName, CancellationToken.None);
        var queueUrl = createQueueResponse.QueueUrl;

        await using var publisher = new SqsPublisher<TestMessage>(_sqsClient, queueUrl);
        await using var consumer = new SqsConsumer<TestMessage>(_sqsClient, queueUrl);

        var tcs = new TaskCompletionSource<bool>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            tcs.TrySetResult(true);
            throw new Exception("Test Exception");
        }, CancellationToken.None);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act
        await publisher.PublishAsync(new TestMessage { Id = 1, Content = "Exception Test" }, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        // The consumer should handle the exception and continue
        await consumer.StopAsync(CancellationToken.None);
    }
}

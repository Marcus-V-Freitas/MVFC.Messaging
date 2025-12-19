namespace MVFC.Messaging.Tests.TestProviders.Confluent.Kafka;

public sealed class KafkaIntegrationTests(KafkaFixture fixture, ITestOutputHelper output) : IClassFixture<KafkaFixture>
{
    private readonly KafkaFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Should_PublishAndConsume_SingleMessage()
    {
        // Arrange
        const string topic = "test-topic";
        const string groupId = "test-group";
        var connectioString = _fixture.ConnectionString();

        await using var publisher = new KafkaPublisher<TestMessage>(connectioString, topic);
        await using var consumer = new KafkaConsumer<TestMessage>(connectioString, topic, groupId);

        var tcs = new TaskCompletionSource<TestMessage>();
        await consumer.StartAsync(async (msg, ct) =>
        {
            _output.WriteLine($"Received: {msg.Content}");
            tcs.SetResult(msg);
        }, CancellationToken.None);

        await Task.Delay(2000);

        // Act
        var sentMessage = new TestMessage { Id = 1, Content = "Kafka Test" };
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
        const string topic = "test-batch-topic";
        const string groupId = "test-batch-group";
        var connectioString = _fixture.ConnectionString();

        await using var publisher = new KafkaPublisher<TestMessage>(connectioString, topic);
        await using var consumer = new KafkaConsumer<TestMessage>(connectioString, topic, groupId);

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
            new TestMessage { Id = 1, Content = "Kafka Batch 1" },
            new TestMessage { Id = 2, Content = "Kafka Batch 2" },
            new TestMessage { Id = 3, Content = "Kafka Batch 3" }
        };

        await publisher.PublishBatchAsync(messages, CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        receivedMessages.Count.Should().Be(3);

        await consumer.StopAsync();
    }
}
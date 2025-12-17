namespace MVFC.Messaging.Confluent.Kafka;

public sealed class KafkaConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    public KafkaConsumer(string bootstrapServers, string topic, string groupId)
    {
        _topic = topic;
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    protected override Task StartInternalAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(_topic);

        _consumeTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(_cts.Token);

                    if (consumeResult?.Message?.Value != null)
                    {
                        var message = JsonSerializer.Deserialize<T>(consumeResult.Message.Value);

                        if (Handler != null && message != null)
                        {
                            await Handler(message, _cts.Token);
                        }

                        _consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Log error
                }
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask;
            }
            catch { }
        }

        _consumer?.Close();
        _consumer?.Dispose();
        _cts?.Dispose();
    }
}
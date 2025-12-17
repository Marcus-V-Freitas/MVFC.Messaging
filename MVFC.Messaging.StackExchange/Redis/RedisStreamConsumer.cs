namespace MVFC.Messaging.StackExchange.Redis;

public sealed class RedisStreamConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _streamKey;
    private readonly string _consumerGroup;
    private readonly string _consumerName;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    public RedisStreamConsumer(string connectionString, string streamKey, string consumerGroup, string? consumerName = null)
    {
        _streamKey = streamKey;
        _consumerGroup = consumerGroup;
        _consumerName = consumerName ?? Guid.NewGuid().ToString();
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(_streamKey, _consumerGroup, StreamPosition.NewMessages);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group já existe
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _consumeTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _db.StreamReadGroupAsync(
                        _streamKey,
                        _consumerGroup,
                        _consumerName,
                        ">",
                        count: 10);

                    foreach (var entry in result)
                    {
                        try
                        {
                            var data = entry.Values.FirstOrDefault(x => x.Name == "data").Value;
                            if (!data.IsNullOrEmpty)
                            {
                                var message = JsonSerializer.Deserialize<T>(data.ToString());

                                if (Handler != null && message != null)
                                {
                                    await Handler(message, _cts.Token);
                                }

                                await _db.StreamAcknowledgeAsync(_streamKey, _consumerGroup, entry.Id);
                            }
                        }
                        catch (Exception)
                        {
                            // Log error
                        }
                    }

                    if (result.Length == 0)
                    {
                        await Task.Delay(100, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);
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

        await _redis.CloseAsync();
        _redis.Dispose();
        _cts?.Dispose();
    }
}
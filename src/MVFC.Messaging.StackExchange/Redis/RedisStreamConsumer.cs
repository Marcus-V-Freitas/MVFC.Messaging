namespace MVFC.Messaging.StackExchange.Redis;

public sealed class RedisStreamConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private const int ReadCount = 10;
    private const int PollingDelayMilliseconds = 100;
    private const string DataFieldName = "data";

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
        _consumerName = consumerName ?? GenerateConsumerName();
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }

    private static string GenerateConsumerName()
    {
        return Guid.NewGuid().ToString();
    }

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        await EnsureConsumerGroupExistsAsync();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ExecuteConsumeLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task EnsureConsumerGroupExistsAsync()
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(
                _streamKey,
                _consumerGroup,
                StreamPosition.NewMessages);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists; ignore the exception.
        }
    }

    private async Task ExecuteConsumeLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ReadAndProcessMessagesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReadAndProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var streamEntries = await ReadMessagesFromStreamAsync();

        if (streamEntries.Length == 0)
        {
            await Task.Delay(PollingDelayMilliseconds, cancellationToken);
            return;
        }

        foreach (var entry in streamEntries)
        {
            await ProcessStreamEntryAsync(entry, cancellationToken);
        }
    }

    private async Task<StreamEntry[]> ReadMessagesFromStreamAsync()
    {
        return await _db.StreamReadGroupAsync(
            _streamKey,
            _consumerGroup,
            _consumerName,
            StreamPosition.NewMessages,
            count: ReadCount);
    }

    private async Task ProcessStreamEntryAsync(StreamEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var messageData = ExtractMessageData(entry);

            if (IsValidMessageData(messageData))
            {
                var message = DeserializeMessage(messageData);

                if (ShouldInvokeHandler(message))
                {
                    await Handler!(message!, cancellationToken);
                }

                await AcknowledgeMessageAsync(entry.Id);
            }
        }
        catch
        {
            // Handler exceptions are ignored to prevent blocking the consumer loop.
        }
    }

    private static RedisValue ExtractMessageData(StreamEntry entry) => 
        entry.Values.FirstOrDefault(x => x.Name == DataFieldName).Value;

    private static bool IsValidMessageData(RedisValue data) => 
        !data.IsNullOrEmpty;

    private static T? DeserializeMessage(RedisValue messageData) => 
        JsonSerializer.Deserialize<T>(messageData.ToString());

    private bool ShouldInvokeHandler(T? message)
    {
        return Handler is not null && message is not null;
    }

    private async Task AcknowledgeMessageAsync(RedisValue messageId)
    {
        await _db.StreamAcknowledgeAsync(_streamKey, _consumerGroup, messageId);
    }

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts!.CancelAsync();

        if (_consumeTask is not null)
        {
            await AwaitConsumeTaskCompletionAsync();
        }

        await CloseRedisConnectionAsync();
        _cts?.Dispose();
    }

    private async Task AwaitConsumeTaskCompletionAsync()
    {
        try
        {
            await _consumeTask!;
        }
        catch
        {
            // Ignore exceptions during disposal
        }
    }

    private async Task CloseRedisConnectionAsync()
    {
        await _redis.CloseAsync();
        await _redis.DisposeAsync();
    }
}
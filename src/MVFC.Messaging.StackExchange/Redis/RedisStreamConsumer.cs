namespace MVFC.Messaging.StackExchange.Redis;

public sealed class RedisStreamConsumer<T> : MessageConsumerBase<T>, IAsyncDisposable
{
    private const int READ_COUNT = 10;
    private const int POLLING_DELAY_MILLISECONDS = 100;
    private const string DATA_FIELD_NAME = "data";

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
        => Guid.NewGuid().ToString();

    protected override async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        await EnsureConsumerGroupExistsAsync().ConfigureAwait(false);

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
                StreamPosition.NewMessages).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
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
                await ReadAndProcessMessagesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReadAndProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var streamEntries = await ReadMessagesFromStreamAsync().ConfigureAwait(false);

        if (streamEntries.Length == 0)
        {
            await Task.Delay(POLLING_DELAY_MILLISECONDS, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var entry in streamEntries)
        {
            await ProcessStreamEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<StreamEntry[]> ReadMessagesFromStreamAsync()
    {
        return await _db.StreamReadGroupAsync(
            _streamKey,
            _consumerGroup,
            _consumerName,
            StreamPosition.NewMessages,
            count: READ_COUNT).ConfigureAwait(false);
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
                    await Handler!(message!, cancellationToken).ConfigureAwait(false);
                }

                await AcknowledgeMessageAsync(entry.Id).ConfigureAwait(false);
            }
        }
        catch
        {
            // Handler exceptions are ignored to prevent blocking the consumer loop.
        }
    }

    private static RedisValue ExtractMessageData(StreamEntry entry) =>
        entry.Values.FirstOrDefault(x => x.Name == DATA_FIELD_NAME).Value;

    private static bool IsValidMessageData(RedisValue data) =>
        !data.IsNullOrEmpty;

    private static T? DeserializeMessage(RedisValue messageData) =>
        JsonSerializer.Deserialize<T>(messageData.ToString());

    private bool ShouldInvokeHandler(T? message) =>
        Handler is not null && message is not null;

    private async Task AcknowledgeMessageAsync(RedisValue messageId) =>
        await _db.StreamAcknowledgeAsync(_streamKey, _consumerGroup, messageId).ConfigureAwait(false);

    protected override Task StopInternalAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts!.CancelAsync().ConfigureAwait(false);

        if (_consumeTask is not null)
        {
            await AwaitConsumeTaskCompletionAsync().ConfigureAwait(false);
        }

        await CloseRedisConnectionAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }

    private async Task AwaitConsumeTaskCompletionAsync()
    {
        try
        {
            await _consumeTask!.ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions during disposal
        }
    }

    private async Task CloseRedisConnectionAsync()
    {
        await _redis.CloseAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }
}

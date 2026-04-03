namespace MVFC.Messaging.StackExchange.Redis;

public sealed class RedisStreamPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private const string DATA_FIELD_NAME = "data";

    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _streamKey;

    public RedisStreamPublisher(string connectionString, string streamKey)
    {
        _streamKey = streamKey;
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var streamEntry = CreateStreamEntry(message);
        await _db.StreamAddAsync(_streamKey, streamEntry).ConfigureAwait(false);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages,
        CancellationToken cancellationToken)
    {
        var publishTasks = CreatePublishTasks(messages, cancellationToken);
        await Task.WhenAll(publishTasks).ConfigureAwait(false);
    }

    private IEnumerable<Task> CreatePublishTasks(
        IEnumerable<T> messages,
        CancellationToken cancellationToken) =>
            messages.Select(message => PublishInternalAsync(message, cancellationToken));

    private static NameValueEntry[] CreateStreamEntry(T message)
    {
        var messageData = SerializeMessage(message);
        return [new NameValueEntry(DATA_FIELD_NAME, messageData)];
    }

    private static string SerializeMessage(T message) =>
        JsonSerializer.Serialize(message);

    public async ValueTask DisposeAsync() =>
        await CloseRedisConnectionAsync().ConfigureAwait(false);

    private async Task CloseRedisConnectionAsync()
    {
        await _redis.CloseAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }
}

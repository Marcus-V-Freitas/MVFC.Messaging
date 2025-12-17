namespace MVFC.Messaging.StackExchange.Redis;

public sealed class RedisStreamPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
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
        var json = JsonSerializer.Serialize(message);
        var entry = new NameValueEntry("data", json);
        await _db.StreamAddAsync(_streamKey, [entry]);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        var tasks = messages.Select(msg => PublishInternalAsync(msg, cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async ValueTask DisposeAsync()
    {
        await _redis.CloseAsync();
        _redis.Dispose();
    }
}
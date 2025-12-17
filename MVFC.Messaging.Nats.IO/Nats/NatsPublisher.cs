namespace MVFC.Messaging.Nats.IO.Nats;

public sealed class NatsPublisher<T> : MessagePublisherBase<T>, IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly string _subject;

    public NatsPublisher(string url, string subject)
    {
        _subject = subject;
        var opts = new NatsOpts { Url = url };
        _connection = new NatsConnection(opts);
    }

    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        await _connection.PublishAsync(_subject, json, cancellationToken: cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        var tasks = messages.Select(msg => PublishInternalAsync(msg, cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

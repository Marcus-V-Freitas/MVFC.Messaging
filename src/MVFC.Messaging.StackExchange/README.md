# MVFC.Messaging.StackExchange

Biblioteca para integração simplificada com Redis Streams usando StackExchange.Redis, utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.StackExchange
```

## Configuração

Configure sua conexão Redis via string de conexão (exemplo: `localhost:6379`).  
Para testes locais, utilize o [Redis](https://redis.io/download/) ou Docker.

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.StackExchange.Redis;

const string streamKey = "test-stream";
const string consumerGroup = "test-group";
var connectionString = "<sua-connection-string-redis>";

await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, streamKey);
await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, streamKey, consumerGroup);

var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(1000); // Aguarda o consumidor inicializar

var sentMessage = new TestMessage { Id = 1, Content = "Redis Stream Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
const string streamKey = "test-batch-stream";
const string consumerGroup = "test-batch-group";
var connectionString = "<sua-connection-string-redis>";

await using var publisher = new RedisStreamPublisher<TestMessage>(connectionString, streamKey);
await using var consumer = new RedisStreamConsumer<TestMessage>(connectionString, streamKey, consumerGroup);

var receivedMessages = new List<TestMessage>();
var tcs = new TaskCompletionSource<bool>();

await consumer.StartAsync(async (msg, ct) =>
{
    lock (receivedMessages)
    {
        receivedMessages.Add(msg);
        Console.WriteLine($"Recebido {receivedMessages.Count}: {msg.Content}");
        if (receivedMessages.Count == 3)
            tcs.SetResult(true);
    }
}, CancellationToken.None);

await Task.Delay(1000);

var messages = new[]
{
    new TestMessage { Id = 1, Content = "Redis Batch 1" },
    new TestMessage { Id = 2, Content = "Redis Batch 2" },
    new TestMessage { Id = 3, Content = "Redis Batch 3" }
};

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
await consumer.StopAsync();
```

## Recursos

- **RedisStreamPublisher**: Publica mensagens (simples ou em lote) em um stream Redis.
- **RedisStreamConsumer**: Consome mensagens de um stream Redis de forma assíncrona.
# MVFC.Messaging.NatsIO

Biblioteca para integração simplificada com NATS.io, utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.NatsIO
```

## Configuração

Configure sua conexão NATS via string de conexão (exemplo: `nats://localhost:4222`).  
Para testes locais, utilize o [NATS Server](https://docs.nats.io/running-a-nats-service/introduction) ou Docker.

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.NatsIO.Nats;

const string subject = "test.subject";
var connectionString = "<sua-connection-string-nats>";

await using var publisher = new NatsPublisher<TestMessage>(connectionString, subject);
await using var consumer = new NatsConsumer<TestMessage>(connectionString, subject);

var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(1000); // Aguarda o consumidor inicializar

var sentMessage = new TestMessage { Id = 1, Content = "NATS Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
const string subject = "test.batch.subject";
var connectionString = "<sua-connection-string-nats>";

await using var publisher = new NatsPublisher<TestMessage>(connectionString, subject);
await using var consumer = new NatsConsumer<TestMessage>(connectionString, subject);

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
    new TestMessage { Id = 1, Content = "NATS Batch 1" },
    new TestMessage { Id = 2, Content = "NATS Batch 2" },
    new TestMessage { Id = 3, Content = "NATS Batch 3" }
};

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
await consumer.StopAsync();
```

## Recursos

- **NatsPublisher**: Publica mensagens (simples ou em lote) em um subject NATS.
- **NatsConsumer**: Consome mensagens de um subject NATS de forma assíncrona.
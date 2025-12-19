# MVFC.Messaging.InMemory

Biblioteca para mensageria in-memory, ideal para testes, desenvolvimento local e cenários sem dependências externas.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.InMemory
```

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.InMemory.Memory;
using System.Threading.Channels;

var channel = Channel.CreateUnbounded<TestMessage>();
var publisher = new InMemoryPublisher<TestMessage>(channel);
var consumer = new InMemoryConsumer<TestMessage>(channel);

var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

var sentMessage = new TestMessage { Id = 1, Content = "Memory Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
using MVFC.Messaging.InMemory.Memory;
using System.Threading.Channels;

var channel = Channel.CreateUnbounded<TestMessage>();
var publisher = new InMemoryPublisher<TestMessage>(channel);
var consumer = new InMemoryConsumer<TestMessage>(channel);

var receivedMessages = new List<TestMessage>();
var tcs = new TaskCompletionSource<bool>();

await consumer.StartAsync(async (msg, ct) =>
{
    lock (receivedMessages)
    {
        receivedMessages.Add(msg);
        if (receivedMessages.Count == 5)
            tcs.SetResult(true);
    }
}, CancellationToken.None);

var messages = Enumerable.Range(1, 5)
    .Select(i => new TestMessage { Id = i, Content = $"Message {i}" })
    .ToArray();

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
await consumer.StopAsync();
```

## Recursos

- **InMemoryPublisher**: Publica mensagens (simples ou em lote) em um canal in-memory.
- **InMemoryConsumer**: Consome mensagens de um canal in-memory de forma assíncrona.
# MVFC.Messaging.GCP

Biblioteca para integração simplificada com Google Cloud Pub/Sub, utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.GCP
```

## Configuração

Configure as credenciais do GCP via variáveis de ambiente (`GOOGLE_APPLICATION_CREDENTIALS`) ou utilize o [Pub/Sub Emulator](https://cloud.google.com/pubsub/docs/emulator) para testes locais.

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.GCP.PubSub;

const string projectId = "test-project";
const string topicId = "test-topic";
const string subscriptionId = "test-subscription";

// Publisher e Consumer
await using var publisher = new PubSubPublisher<TestMessage>(projectId, topicId);
await using var consumer = new PubSubConsumer<TestMessage>(projectId, subscriptionId);

// Consumidor assíncrono
var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

// Publica mensagem
var sentMessage = new TestMessage { Id = 100, Content = "PubSub Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

// Aguarda consumo
var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

// Parada do consumidor
await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
const string batchTopicId = "batch-topic";
const string batchSubscriptionId = "batch-subscription";

await using var publisher = new PubSubPublisher<TestMessage>(projectId, batchTopicId);
await using var consumer = new PubSubConsumer<TestMessage>(projectId, batchSubscriptionId);

var receivedMessages = new List<TestMessage>();
var tcs = new TaskCompletionSource<bool>();

await consumer.StartAsync(async (msg, ct) =>
{
    receivedMessages.Add(msg);
    if (receivedMessages.Count == 3)
        tcs.SetResult(true);
}, CancellationToken.None);

var messages = new[]
{
    new TestMessage { Id = 1, Content = "Batch 1" },
    new TestMessage { Id = 2, Content = "Batch 2" },
    new TestMessage { Id = 3, Content = "Batch 3" }
};

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
await consumer.StopAsync();
```

## Recursos

- **PubSubPublisher**: Publica mensagens (simples ou em lote) em um tópico Pub/Sub.
- **PubSubConsumer**: Consome mensagens de uma subscription Pub/Sub de forma assíncrona.
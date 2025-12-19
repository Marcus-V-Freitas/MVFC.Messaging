# MVFC.Messaging.Confluent

Biblioteca para integração simplificada com Apache Kafka (Confluent), utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.Confluent
```

## Configuração

Configure sua conexão Kafka via string de conexão (exemplo: `localhost:9092`).  
Para testes locais, utilize o [Confluent Platform](https://docs.confluent.io/platform/current/quickstart/index.html) ou Docker.

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.Confluent.Kafka;

const string topic = "test-topic";
const string groupId = "test-group";
var connectionString = "<sua-connection-string-kafka>";

await using var publisher = new KafkaPublisher<TestMessage>(connectionString, topic);
await using var consumer = new KafkaConsumer<TestMessage>(connectionString, topic, groupId);

var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(2000); // Aguarda o consumidor inicializar

var sentMessage = new TestMessage { Id = 1, Content = "Kafka Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
const string topic = "test-batch-topic";
const string groupId = "test-batch-group";
var connectionString = "<sua-connection-string-kafka>";

await using var publisher = new KafkaPublisher<TestMessage>(connectionString, topic);
await using var consumer = new KafkaConsumer<TestMessage>(connectionString, topic, groupId);

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

await Task.Delay(2000);

var messages = new[]
{
    new TestMessage { Id = 1, Content = "Kafka Batch 1" },
    new TestMessage { Id = 2, Content = "Kafka Batch 2" },
    new TestMessage { Id = 3, Content = "Kafka Batch 3" }
};

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
await consumer.StopAsync();
```

## Recursos

- **KafkaPublisher**: Publica mensagens (simples ou em lote) em um tópico Kafka.
- **KafkaConsumer**: Consome mensagens de um tópico Kafka de forma assíncrona.
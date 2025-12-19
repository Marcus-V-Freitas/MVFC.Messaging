# MVFC.Messaging.RabbitMQ

Biblioteca para integração simplificada com RabbitMQ, utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.RabbitMQ
```

## Configuração

Configure sua conexão RabbitMQ via string de conexão (exemplo: `amqp://guest:guest@localhost:5672/`).  
Para testes locais, utilize o [RabbitMQ](https://www.rabbitmq.com/download.html) ou Docker.

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

const string queueName = "test-rabbit-queue";
var connectionString = "<sua-connection-string-rabbitmq>";

await using var publisher = new RabbitMqPublisher<TestMessage>(connectionString, queueName);
await using var consumer = new RabbitMqConsumer<TestMessage>(connectionString, queueName);

var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(1000); // Aguarda o consumidor inicializar

var sentMessage = new TestMessage { Id = 1, Content = "RabbitMQ Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
const string queueName = "test-rabbit-batch";
var connectionString = "<sua-connection-string-rabbitmq>";

await using var publisher = new RabbitMqPublisher<TestMessage>(connectionString, queueName);
await using var consumer = new RabbitMqConsumer<TestMessage>(connectionString, queueName);

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
    new TestMessage { Id = 1, Content = "Batch 1" },
    new TestMessage { Id = 2, Content = "Batch 2" },
    new TestMessage { Id = 3, Content = "Batch 3" }
};

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
await consumer.StopAsync();
```

## Recursos

- **RabbitMqPublisher**: Publica mensagens (simples ou em lote) em uma fila RabbitMQ.
- **RabbitMqConsumer**: Consome mensagens de uma fila RabbitMQ de forma assíncrona.
# MVFC.Messaging.Azure

Biblioteca para integração simplificada com Azure Service Bus, utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.Azure
```

## Configuração

Configure sua conexão com o Azure Service Bus via string de conexão obtida no portal Azure.  
Veja mais detalhes na [documentação oficial do Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/).

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using MVFC.Messaging.Azure.ServiceBus;

const string queueName = "queue.1";
var connectionString = "<sua-connection-string-do-service-bus>";

await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, queueName);
await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, queueName);

var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(2000); // Aguarda o consumidor inicializar

var sentMessage = new TestMessage { Id = 1, Content = "ServiceBus Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
const string queueName = "queue.1";
var connectionString = "<sua-connection-string-do-service-bus>";

await using var publisher = new ServiceBusPublisher<TestMessage>(connectionString, queueName);
await using var consumer = new ServiceBusConsumer<TestMessage>(connectionString, queueName);

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
    new TestMessage { Id = 1, Content = "Batch 1" },
    new TestMessage { Id = 2, Content = "Batch 2" },
    new TestMessage { Id = 3, Content = "Batch 3" }
};

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
await consumer.StopAsync();
```

## Recursos

- **ServiceBusPublisher**: Publica mensagens (simples ou em lote) em uma fila do Azure Service Bus.
- **ServiceBusConsumer**: Consome mensagens de uma fila do Azure Service Bus de forma assíncrona.
# MVFC.Messaging.AWS

Biblioteca para integração simplificada com Amazon SQS, utilizando padrões modernos de C#.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.AWS
```

## Configuração

Configure suas credenciais AWS via variáveis de ambiente, arquivo de configuração ou perfil padrão AWS.  
Para testes locais, pode ser usado o [LocalStack](https://github.com/localstack/localstack).

## Uso Básico

### Publicando e Consumindo uma Mensagem

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

// Configuração do cliente SQS (exemplo usando LocalStack)
var sqsClient = new AmazonSQSClient(new AmazonSQSConfig { ServiceURL = "http://localhost:4566" });

// Criação da fila
var queueName = $"test-queue-{Guid.NewGuid()}";
var createQueueResponse = await sqsClient.CreateQueueAsync(queueName);
var queueUrl = createQueueResponse.QueueUrl;

// Publisher e Consumer
await using var publisher = new SqsPublisher<TestMessage>(sqsClient, queueUrl);
await using var consumer = new SqsConsumer<TestMessage>(sqsClient, queueUrl);

// Consumidor assíncrono
var tcs = new TaskCompletionSource<TestMessage>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: {msg.Content}");
    tcs.SetResult(msg);
}, CancellationToken.None);

// Publica mensagem
var sentMessage = new TestMessage { Id = 1, Content = "SQS Test" };
await publisher.PublishAsync(sentMessage, CancellationToken.None);

// Aguarda consumo
var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

// Parada do consumidor
await consumer.StopAsync();
```

### Publicando e Consumindo Mensagens em Lote

```csharp
// ... configuração igual ao exemplo anterior ...

var messages = new[]
{
    new TestMessage { Id = 1, Content = "Batch 1" },
    new TestMessage { Id = 2, Content = "Batch 2" },
    new TestMessage { Id = 3, Content = "Batch 3" }
};

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

await publisher.PublishBatchAsync(messages, CancellationToken.None);
await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
await consumer.StopAsync();
```

## Recursos

- **SqsPublisher**: Publica mensagens (simples ou em lote) em uma fila SQS.
- **SqsConsumer**: Consome mensagens de uma fila SQS de forma assíncrona.
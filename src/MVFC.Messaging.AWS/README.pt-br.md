# MVFC.Messaging.AWS

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.AWS)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.AWS)

Provedor de mensageria .NET para **Amazon Simple Queue Service (SQS)**, construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `SqsPublisher<T>` e `SqsConsumer<T>` para publicação e consumo de mensagens serializadas em JSON em filas SQS com uma API limpa e async-first.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.AWS](https://www.nuget.org/packages/MVFC.Messaging.AWS) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.AWS) |

## Instalação

```sh
dotnet add package MVFC.Messaging.AWS
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `AWSSDK.SQS`.

## Configuração

### Credenciais AWS

O `SqsPublisher<T>` e o `SqsConsumer<T>` recebem uma instância de `IAmazonSQS`, então você controla como as credenciais são fornecidas. Abordagens comuns:

| Método | Descrição |
|---|---|
| **Variáveis de ambiente** | Defina `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` e opcionalmente `AWS_SESSION_TOKEN` |
| **Arquivo de credenciais** | `~/.aws/credentials` com perfil `[default]` |
| **IAM Role** | No EC2, ECS ou Lambda, o SDK utiliza automaticamente a role da instância/tarefa |
| **LocalStack** | Para desenvolvimento local, aponte `ServiceURL` para `http://localhost:4566` |

### Criando o Cliente SQS

```csharp
using Amazon.SQS;

// Produção — usa a cadeia de credenciais padrão (vars de ambiente, IAM role, etc.)
var sqsClient = new AmazonSQSClient();

// Desenvolvimento local com LocalStack
var sqsClient = new AmazonSQSClient(new AmazonSQSConfig
{
    ServiceURL = "http://localhost:4566"
});
```

### URL da Fila

Tanto o `SqsPublisher<T>` quanto o `SqsConsumer<T>` requerem a **URL da fila** (não o nome da fila). Você pode obtê-la criando uma fila ou chamando `GetQueueUrlAsync`:

```csharp
var createResponse = await sqsClient.CreateQueueAsync("my-queue");
var queueUrl = createResponse.QueueUrl;
// ex: "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue"
```

## Uso

### Publicando uma Única Mensagem

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

var sqsClient = new AmazonSQSClient();
var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue";

await using var publisher = new SqsPublisher<OrderCreated>(sqsClient, queueUrl);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

A mensagem é serializada em JSON usando `System.Text.Json` e enviada como corpo da mensagem SQS.

### Publicando um Lote

O SQS suporta o envio de até 10 mensagens em uma única chamada `SendMessageBatch`. O método `PublishBatchAsync` utiliza esta API nativa de batch para performance otimizada:

```csharp
var orders = new[]
{
    new OrderCreated(1, "Teclado", 149.90m),
    new OrderCreated(2, "Mouse", 59.90m),
    new OrderCreated(3, "Monitor", 899.00m)
};

await publisher.PublishBatchAsync(orders);
```

### Consumindo Mensagens

O consumer executa um loop de polling em background que chama `ReceiveMessage` com long polling (espera de 5 segundos), processa cada mensagem através do seu handler e automaticamente deleta a mensagem da fila após processamento bem-sucedido:

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

var sqsClient = new AmazonSQSClient();
var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue";

await using var consumer = new SqsConsumer<OrderCreated>(sqsClient, queueUrl);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Faz polling de até **10 mensagens** por requisição (máximo do SQS).
- Usa **long polling** com tempo de espera de 5 segundos para reduzir respostas vazias.
- Após execução bem-sucedida do handler, a mensagem é **automaticamente deletada** da fila.
- Se o handler lançar uma exceção, a mensagem **não é deletada** e se torna visível novamente após o timeout de visibilidade da fila.
- O loop de polling executa até que `StopAsync` seja chamado ou o `CancellationToken` seja cancelado.

### Exemplo Completo de Publicação + Consumo

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

// Setup
var sqsClient = new AmazonSQSClient(new AmazonSQSConfig { ServiceURL = "http://localhost:4566" });
var createResponse = await sqsClient.CreateQueueAsync($"orders-{Guid.NewGuid()}");
var queueUrl = createResponse.QueueUrl;

await using var publisher = new SqsPublisher<OrderCreated>(sqsClient, queueUrl);
await using var consumer = new SqsConsumer<OrderCreated>(sqsClient, queueUrl);

// Inicia consumo
var received = new TaskCompletionSource<OrderCreated>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: Pedido #{msg.OrderId} — {msg.Product}");
    received.SetResult(msg);
}, CancellationToken.None);

// Publica
await publisher.PublishAsync(new OrderCreated(42, "Teclado", 149.90m));

// Aguarda a mensagem ser consumida
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));

// Limpeza
await consumer.StopAsync();
```

## Referência da API

### SqsPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `SqsPublisher<T>(IAmazonSQS sqsClient, string queueUrl)` | Instância do cliente SQS e URL da fila para publicar |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa a mensagem em JSON e envia para a fila SQS |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Envia até 10 mensagens em uma única requisição batch do SQS |
| `DisposeAsync()` | Libera o cliente `IAmazonSQS` subjacente |

### SqsConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `SqsConsumer<T>(IAmazonSQS sqsClient, string queueUrl)` | Instância do cliente SQS e URL da fila para consumir |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Inicia o loop de polling em background |
| `StopAsync(CancellationToken ct)` | Cancela o loop de polling e aguarda a conclusão |
| `DisposeAsync()` | Libera o cliente `IAmazonSQS` subjacente |

## Requisitos

- .NET 9.0+
- `AWSSDK.SQS` (instalado automaticamente)

## Licença

[Apache-2.0](../../LICENSE)

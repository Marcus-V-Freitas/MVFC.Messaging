# MVFC.Messaging.GCP

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.GCP)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.GCP)

Provedor de mensageria .NET para **Google Cloud Pub/Sub**, construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `PubSubPublisher<T>` e `PubSubConsumer<T>` para publicação e consumo de mensagens serializadas em JSON usando a biblioteca oficial do Google Cloud com suporte embutido a emulador.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.GCP](https://www.nuget.org/packages/MVFC.Messaging.GCP) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.GCP) |

## Instalação

```sh
dotnet add package MVFC.Messaging.GCP
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `Google.Cloud.PubSub.V1`.

## Configuração

### Autenticação

As bibliotecas cliente do Google Cloud usam [Application Default Credentials (ADC)](https://cloud.google.com/docs/authentication/application-default-credentials). Configurações comuns:

| Método | Descrição |
|---|---|
| **`gcloud auth application-default login`** | Para desenvolvimento local — armazena credenciais em `~/.config/gcloud/` |
| **Service Account JSON** | Defina a variável de ambiente `GOOGLE_APPLICATION_CREDENTIALS` com o caminho do arquivo JSON da chave |
| **Workload Identity** | No GKE, Cloud Run ou Compute Engine — descoberta automática de credenciais |
| **Emulador** | Defina `PUBSUB_EMULATOR_HOST=localhost:8085` para testes locais |

### Detecção de Emulador

Tanto o `PubSubPublisher<T>` quanto o `PubSubConsumer<T>` são configurados com `EmulatorDetection.EmulatorOrProduction`. Isso significa:
- Se `PUBSUB_EMULATOR_HOST` estiver definido → conecta ao emulador.
- Caso contrário → conecta ao Google Cloud Pub/Sub de produção.

Nenhuma mudança de código necessária entre ambientes local e produção.

### Parâmetros do Construtor

- **Publisher:** `projectId` (seu projeto GCP) e `topicId` (nome do tópico Pub/Sub).
- **Consumer:** `projectId` e `subscriptionId` (nome da subscription Pub/Sub).

### Exemplo appsettings.json

```json
{
  "GCP": {
    "ProjectId": "my-gcp-project",
    "TopicId": "orders",
    "SubscriptionId": "orders-subscription"
  }
}
```

```csharp
var projectId = builder.Configuration["GCP:ProjectId"]!;
var topicId = builder.Configuration["GCP:TopicId"]!;
var subscriptionId = builder.Configuration["GCP:SubscriptionId"]!;
```

## Uso

### Publicando uma Única Mensagem

```csharp
using MVFC.Messaging.GCP.PubSub;

var projectId = "my-gcp-project";
var topicId = "orders";

await using var publisher = new PubSubPublisher<OrderCreated>(projectId, topicId);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

A mensagem é serializada em JSON e publicada como dados da mensagem Pub/Sub.

### Publicando um Lote

A publicação em lote envia todas as mensagens concorrentemente usando `Task.WhenAll`:

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

O consumer encapsula o `SubscriberClient` do Google Cloud, que gerencia a entrega de mensagens baseada em pull com controle de fluxo automático. Cada mensagem recebe uma resposta **Ack** (sucesso) ou **Nack** (falha):

```csharp
using MVFC.Messaging.GCP.PubSub;

var projectId = "my-gcp-project";
var subscriptionId = "orders-subscription";

await using var consumer = new PubSubConsumer<OrderCreated>(projectId, subscriptionId);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Usa o `SubscriberClient.StartAsync` do Google Cloud para entrega gerenciada de mensagens.
- Execução bem-sucedida do handler resulta em um **Ack** — a mensagem é removida da subscription.
- Se o handler lançar uma exceção, um **Nack** é enviado — a mensagem será reentregue.
- Um atraso de inicialização de 1 segundo garante que o subscriber esteja totalmente pronto antes de retornar.
- `StopAsync` para o subscriber gracefully e aguarda todos os handlers pendentes completarem.

### Exemplo Completo de Publicação + Consumo

```csharp
using MVFC.Messaging.GCP.PubSub;

// Usando emulador: defina PUBSUB_EMULATOR_HOST=localhost:8085
var projectId = "my-gcp-project";
var topicId = "orders";
var subscriptionId = "orders-subscription";

await using var publisher = new PubSubPublisher<OrderCreated>(projectId, topicId);
await using var consumer = new PubSubConsumer<OrderCreated>(projectId, subscriptionId);

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
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));

// Limpeza
await consumer.StopAsync();
```

## Referência da API

### PubSubPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `PubSubPublisher<T>(string projectId, string topicId)` | ID do projeto GCP e nome do tópico Pub/Sub |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa a mensagem em JSON e publica no tópico |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publica todas as mensagens concorrentemente |
| `DisposeAsync()` | Encerra o `PublisherClient` subjacente |

### PubSubConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `PubSubConsumer<T>(string projectId, string subscriptionId)` | ID do projeto GCP e nome da subscription Pub/Sub |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Inicia o subscriber com tratamento Ack/Nack |
| `StopAsync(CancellationToken ct)` | Para o subscriber e aguarda handlers pendentes |
| `DisposeAsync()` | Para o subscriber e ignora exceções de shutdown |

## Requisitos

- .NET 9.0+
- `Google.Cloud.PubSub.V1` (instalado automaticamente)
- Credenciais Google Cloud (ADC) ou emulador Pub/Sub

## Licença

[Apache-2.0](../../LICENSE)

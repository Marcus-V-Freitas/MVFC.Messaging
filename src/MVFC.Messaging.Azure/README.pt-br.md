# MVFC.Messaging.Azure

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Azure)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Azure)

Provedor de mensageria .NET para **Azure Service Bus**, construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `ServiceBusPublisher<T>` e `ServiceBusConsumer<T>` para publicação e consumo de mensagens serializadas em JSON em filas e tópicos com transporte AMQP/TCP.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.Azure](https://www.nuget.org/packages/MVFC.Messaging.Azure) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Azure) |

## Instalação

```sh
dotnet add package MVFC.Messaging.Azure
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `Azure.Messaging.ServiceBus`.

## Configuração

### String de Conexão

Tanto o `ServiceBusPublisher<T>` quanto o `ServiceBusConsumer<T>` aceitam uma **string de conexão** e um **nome de fila ou tópico** no construtor. Você encontra a string de conexão no Portal Azure em seu namespace do Service Bus → **Políticas de acesso compartilhado**.

```
Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<sua-chave>
```

### Transporte

O provedor é configurado para usar **AMQP sobre TCP** (`ServiceBusTransportType.AmqpTcp`) para performance otimizada. É uma conexão TCP direta — sem overhead de WebSocket.

### Exemplo appsettings.json

```json
{
  "Azure": {
    "ServiceBus": {
      "ConnectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=...",
      "QueueName": "orders"
    }
  }
}
```

```csharp
var connectionString = builder.Configuration["Azure:ServiceBus:ConnectionString"]!;
var queueName = builder.Configuration["Azure:ServiceBus:QueueName"]!;
```

## Uso

### Publicando uma Única Mensagem

```csharp
using MVFC.Messaging.Azure.ServiceBus;

var connectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
var queueName = "orders";

await using var publisher = new ServiceBusPublisher<OrderCreated>(connectionString, queueName);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

A mensagem é serializada em JSON e encapsulada em um `ServiceBusMessage`.

### Publicando um Lote

O publisher usa a API nativa `ServiceBusMessageBatch` para operações em lote. Se uma mensagem não couber no batch (limite de tamanho atingido), uma `InvalidOperationException` é lançada:

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

O consumer usa o `ServiceBusProcessor` embutido do Azure Service Bus, que gerencia o polling, concorrência e tratamento de erros internamente. As mensagens **não são auto-completadas** — são explicitamente completadas após execução bem-sucedida do handler:

```csharp
using MVFC.Messaging.Azure.ServiceBus;

var connectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
var queueName = "orders";

await using var consumer = new ServiceBusConsumer<OrderCreated>(connectionString, queueName);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Usa o modelo orientado a eventos do `ServiceBusProcessor` — sem polling manual.
- `AutoCompleteMessages` é definido como **false**; mensagens são completadas explicitamente após sucesso do handler.
- Se o handler lançar uma exceção, a mensagem **não é completada** e será re-tentada com base no max delivery count da fila.
- Eventos de erro são registrados via `Console.WriteLine` (você pode personalizar estendendo a classe).
- `StartAsync` inicia o processor; `StopAsync` o para de forma graceful.

### Exemplo Completo de Publicação + Consumo

```csharp
using MVFC.Messaging.Azure.ServiceBus;

var connectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
var queueName = "orders";

await using var publisher = new ServiceBusPublisher<OrderCreated>(connectionString, queueName);
await using var consumer = new ServiceBusConsumer<OrderCreated>(connectionString, queueName);

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

### ServiceBusPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `ServiceBusPublisher<T>(string connectionString, string queueOrTopicName)` | String de conexão do Service Bus e nome da fila/tópico |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa a mensagem e envia para a fila/tópico |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Cria um batch nativo e envia todas as mensagens em uma operação |
| `DisposeAsync()` | Libera o `ServiceBusSender` e o `ServiceBusClient` |

### ServiceBusConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `ServiceBusConsumer<T>(string connectionString, string queueOrTopicName)` | String de conexão do Service Bus e nome da fila/tópico |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Inicia o processor do Service Bus |
| `StopAsync(CancellationToken ct)` | Para o processor de forma graceful |
| `DisposeAsync()` | Libera o `ServiceBusProcessor` e o `ServiceBusClient` |

## Requisitos

- .NET 9.0+
- `Azure.Messaging.ServiceBus` (instalado automaticamente)

## Licença

[Apache-2.0](../../LICENSE)

# MVFC.Messaging.RabbitMQ

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-10-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.RabbitMQ)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.RabbitMQ)

Provedor de mensageria .NET para **[RabbitMQ](https://www.rabbitmq.com/)**, construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `RabbitMqPublisher<T>` e `RabbitMqConsumer<T>` para publicação e consumo de mensagens serializadas em JSON em filas RabbitMQ com entrega persistente, QoS prefetch e acknowledgment explícito.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.RabbitMQ](https://www.nuget.org/packages/MVFC.Messaging.RabbitMQ) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.RabbitMQ) |

## Instalação

```sh
dotnet add package MVFC.Messaging.RabbitMQ
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `RabbitMQ.Client`.

## Configuração

### String de Conexão

Tanto o `RabbitMqPublisher<T>` quanto o `RabbitMqConsumer<T>` usam um **padrão de factory assíncrono** (`CreateAsync`) e aceitam uma **string de conexão** (URI AMQP) e um **nome de fila**.

```
amqp://guest:guest@localhost:5672/
amqp://user:password@rabbitmq-host:5672/vhost
```

### Configuração da Fila

A fila é **automaticamente declarada** durante a criação com as seguintes configurações:

| Configuração | Valor | Descrição |
|---|---|---|
| `durable` | `true` | A fila sobrevive a reinicializações do broker |
| `exclusive` | `false` | A fila não é exclusiva da conexão |
| `autoDelete` | `false` | A fila não é deletada quando o último consumer desconecta |

### QoS do Consumer

O consumer define um **prefetch count de 10**, o que significa que o broker entrega até 10 mensagens não confirmadas ao consumer de cada vez.

### Exemplo appsettings.json

```json
{
  "RabbitMQ": {
    "ConnectionString": "amqp://guest:guest@localhost:5672/",
    "QueueName": "orders"
  }
}
```

```csharp
var connectionString = builder.Configuration["RabbitMQ:ConnectionString"]!;
var queueName = builder.Configuration["RabbitMQ:QueueName"]!;
```

## Uso

### Padrão de Factory Assíncrono

Diferentemente de outros provedores, o RabbitMQ usa um **método factory assíncrono** em vez de um construtor regular, pois estabelecer a conexão e declarar a fila são operações assíncronas:

```csharp
// ✅ Correto — use CreateAsync
await using var publisher = await RabbitMqPublisher<OrderCreated>.CreateAsync(connectionString, queueName);
await using var consumer = await RabbitMqConsumer<OrderCreated>.CreateAsync(connectionString, queueName);

// ❌ Errado — o construtor é privado
// var publisher = new RabbitMqPublisher<OrderCreated>(...);
```

### Publicando uma Única Mensagem

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

var connectionString = "amqp://guest:guest@localhost:5672/";
var queueName = "orders";

await using var publisher = await RabbitMqPublisher<OrderCreated>.CreateAsync(connectionString, queueName);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

As mensagens são serializadas em JSON, codificadas em UTF-8 e publicadas com `Persistent = true` para sobreviver a reinicializações do broker.

### Publicando um Lote

A publicação em lote envia cada mensagem sequencialmente usando o mesmo canal:

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

O consumer usa `AsyncEventingBasicConsumer` para entrega de mensagens orientada a eventos com acknowledgment explícito:

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

var connectionString = "amqp://guest:guest@localhost:5672/";
var queueName = "orders";

await using var consumer = await RabbitMqConsumer<OrderCreated>.CreateAsync(connectionString, queueName);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Usa `AsyncEventingBasicConsumer` para tratamento de mensagens totalmente assíncrono.
- `autoAck` é definido como **false** — mensagens devem ser explicitamente confirmadas.
- Na execução bem-sucedida do handler, `BasicAckAsync` é chamado — a mensagem é removida da fila.
- Na exceção do handler, `BasicNackAsync` é chamado com `requeue: true` — a mensagem retorna à fila para reentrega.
- O prefetch count do QoS é **10** — o broker entrega até 10 mensagens de cada vez antes de aguardar acks.

### Exemplo Completo de Publicação + Consumo

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

var connectionString = "amqp://guest:guest@localhost:5672/";
var queueName = "orders";

await using var publisher = await RabbitMqPublisher<OrderCreated>.CreateAsync(connectionString, queueName);
await using var consumer = await RabbitMqConsumer<OrderCreated>.CreateAsync(connectionString, queueName);

// Inicia consumo
var received = new TaskCompletionSource<OrderCreated>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: Pedido #{msg.OrderId} — {msg.Product}");
    received.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(1000); // Aguarda inicialização do consumer

// Publica
await publisher.PublishAsync(new OrderCreated(42, "Teclado", 149.90m));

// Aguarda a mensagem ser consumida
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

// Limpeza
await consumer.StopAsync();
```

## Referência da API

### RabbitMqPublisher\<T\>

| Método Factory | Parâmetros |
|---|---|
| `RabbitMqPublisher<T>.CreateAsync(string connectionString, string queueName)` | URI AMQP e nome da fila (fila é auto-declarada) |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa em JSON, codifica em UTF-8 e publica com persistência |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publica cada mensagem sequencialmente |
| `DisposeAsync()` | Fecha e libera o canal e a conexão |

### RabbitMqConsumer\<T\>

| Método Factory | Parâmetros |
|---|---|
| `RabbitMqConsumer<T>.CreateAsync(string connectionString, string queueName)` | URI AMQP e nome da fila (fila é auto-declarada, QoS é configurado) |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Cria um consumer de eventos assíncrono e inicia o consumo |
| `StopAsync(CancellationToken ct)` | No-op (consumer para quando a conexão é liberada) |
| `DisposeAsync()` | Fecha e libera o canal e a conexão |

## Requisitos

- .NET 10.0+
- `RabbitMQ.Client` (instalado automaticamente)
- Um servidor RabbitMQ em execução (ou Docker: `docker run -p 5672:5672 -p 15672:15672 rabbitmq:management`)

## Licença

[Apache-2.0](../../LICENSE)

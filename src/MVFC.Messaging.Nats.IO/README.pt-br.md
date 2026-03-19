# MVFC.Messaging.Nats.IO

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Nats.IO)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Nats.IO)

Provedor de mensageria .NET para **[NATS](https://nats.io/)**, construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `NatsPublisher<T>` e `NatsConsumer<T>` para publicação e consumo de mensagens serializadas em JSON em subjects NATS com a biblioteca oficial [NATS.Client.Core](https://github.com/nats-io/nats.net).

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.Nats.IO](https://www.nuget.org/packages/MVFC.Messaging.Nats.IO) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Nats.IO) |

## Instalação

```sh
dotnet add package MVFC.Messaging.Nats.IO
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `NATS.Client.Core`.

## Configuração

### URL do Servidor NATS

Tanto o `NatsPublisher<T>` quanto o `NatsConsumer<T>` aceitam uma **URL** e um nome de **subject**. A URL aponta para seu servidor NATS:

```
nats://localhost:4222
nats://user:password@nats-server:4222
```

### Subjects

O NATS usa uma hierarquia plana de subjects baseada em strings (ex: `orders.created`, `orders.>`). Você fornece o subject tanto para o publisher quanto para o consumer — subjects correspondentes permitem que as mensagens fluam.

### Exemplo appsettings.json

```json
{
  "Nats": {
    "Url": "nats://localhost:4222",
    "Subject": "orders.created"
  }
}
```

```csharp
var url = builder.Configuration["Nats:Url"]!;
var subject = builder.Configuration["Nats:Subject"]!;
```

## Uso

### Publicando uma Única Mensagem

```csharp
using MVFC.Messaging.Nats.IO.Nats;

var url = "nats://localhost:4222";
var subject = "orders.created";

await using var publisher = new NatsPublisher<OrderCreated>(url, subject);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

A mensagem é serializada em JSON e publicada no subject NATS.

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

O consumer se inscreve no subject NATS e executa um loop em background que produz mensagens conforme elas chegam:

```csharp
using MVFC.Messaging.Nats.IO.Nats;

var url = "nats://localhost:4222";
var subject = "orders.created";

await using var consumer = new NatsConsumer<OrderCreated>(url, subject);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Usa `NatsConnection.SubscribeAsync<string>` para receber mensagens como async stream.
- Mensagens com dados null ou vazios são ignoradas.
- Cada mensagem válida é deserializada de JSON e passada ao handler.
- `OperationCanceledException` no handler é re-lançada para propagação adequada de cancelamento; outras exceções são capturadas para evitar quebrar o loop de consumo.
- `DisposeAsync` cancela o loop de consumo, aguarda a conclusão e libera a conexão NATS.

### Exemplo Completo de Publicação + Consumo

```csharp
using MVFC.Messaging.Nats.IO.Nats;

var url = "nats://localhost:4222";
var subject = "orders.created";

await using var publisher = new NatsPublisher<OrderCreated>(url, subject);
await using var consumer = new NatsConsumer<OrderCreated>(url, subject);

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
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

// Limpeza
await consumer.StopAsync();
```

## Referência da API

### NatsPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `NatsPublisher<T>(string url, string subject)` | URL do servidor NATS e o subject alvo |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa a mensagem em JSON e publica no subject |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publica todas as mensagens concorrentemente |
| `DisposeAsync()` | Libera a `NatsConnection` subjacente |

### NatsConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `NatsConsumer<T>(string url, string subject)` | URL do servidor NATS e o subject para se inscrever |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Se inscreve no subject e inicia o loop de consumo |
| `StopAsync(CancellationToken ct)` | Cancela o loop de consumo |
| `DisposeAsync()` | Cancela, aguarda conclusão e libera a conexão |

## Requisitos

- .NET 9.0+
- `NATS.Client.Core` (instalado automaticamente)
- Um servidor NATS em execução (ou Docker: `docker run -p 4222:4222 nats`)

## Licença

[Apache-2.0](../../LICENSE)

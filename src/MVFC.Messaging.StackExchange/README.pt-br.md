# MVFC.Messaging.StackExchange

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/main/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.StackExchange)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.StackExchange)

Provedor de mensageria .NET para **Redis Streams** via [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis), construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `RedisStreamPublisher<T>` e `RedisStreamConsumer<T>` para publicação e consumo de mensagens serializadas em JSON usando Redis Streams com **consumer groups** e **acknowledgment explícito**.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.StackExchange](https://www.nuget.org/packages/MVFC.Messaging.StackExchange) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.StackExchange) |

## Instalação

```sh
dotnet add package MVFC.Messaging.StackExchange
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `StackExchange.Redis`.

## Configuração

### String de Conexão

Tanto o `RedisStreamPublisher<T>` quanto o `RedisStreamConsumer<T>` aceitam uma **string de conexão** Redis:

```
localhost:6379
redis-host:6379,password=secret,ssl=true
redis-host:6379,abortConnect=false,connectTimeout=5000
```

### Stream Key e Consumer Groups

O publisher escreve em uma **stream key** (como `orders-stream`). O consumer lê da mesma stream key usando um **consumer group** — isso permite que múltiplos consumers compartilhem a carga de trabalho:

- **Stream key:** Um nome único para o Redis Stream (ex: `orders-stream`).
- **Consumer group:** Um grupo nomeado; cada mensagem é entregue a apenas um consumer no grupo.
- **Consumer name:** Um identificador único opcional para esta instância do consumer (auto-gerado se não fornecido).

### Exemplo appsettings.json

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "StreamKey": "orders-stream",
    "ConsumerGroup": "order-processors"
  }
}
```

```csharp
var connectionString = builder.Configuration["Redis:ConnectionString"]!;
var streamKey = builder.Configuration["Redis:StreamKey"]!;
var consumerGroup = builder.Configuration["Redis:ConsumerGroup"]!;
```

## Uso

### Publicando uma Única Mensagem

```csharp
using MVFC.Messaging.StackExchange.Redis;

var connectionString = "localhost:6379";
var streamKey = "orders-stream";

await using var publisher = new RedisStreamPublisher<OrderCreated>(connectionString, streamKey);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

A mensagem é serializada em JSON e adicionada ao stream como uma entrada com campo `data` via `StreamAddAsync`.

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

O consumer usa **consumer groups** do Redis (`StreamReadGroupAsync`) para ler mensagens do stream. Cada mensagem processada com sucesso é explicitamente confirmada:

```csharp
using MVFC.Messaging.StackExchange.Redis;

var connectionString = "localhost:6379";
var streamKey = "orders-stream";
var consumerGroup = "order-processors";

await using var consumer = new RedisStreamConsumer<OrderCreated>(connectionString, streamKey, consumerGroup);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Na inicialização, **cria o consumer group** se não existir (trata o erro `BUSYGROUP` gracefully).
- Lê até **10 mensagens** por poll usando `StreamReadGroupAsync`.
- Quando não há novas mensagens disponíveis, aguarda **100ms** antes de fazer poll novamente.
- O campo `data` de cada mensagem é deserializado de JSON. Entradas null ou vazias são ignoradas.
- Após execução bem-sucedida do handler, `StreamAcknowledgeAsync` é chamado — a mensagem é removida da lista de pendentes do consumer.
- Exceções do handler são capturadas para evitar bloquear o loop de consumo.
- Um **consumer name** único é auto-gerado (UUID) se não fornecido, permitindo múltiplas instâncias no mesmo grupo.
- `DisposeAsync` cancela o loop de consumo, aguarda a conclusão e fecha a conexão Redis.

### Exemplo Completo de Publicação + Consumo

```csharp
using MVFC.Messaging.StackExchange.Redis;

var connectionString = "localhost:6379";
var streamKey = "orders-stream";
var consumerGroup = "order-processors";

await using var publisher = new RedisStreamPublisher<OrderCreated>(connectionString, streamKey);
await using var consumer = new RedisStreamConsumer<OrderCreated>(connectionString, streamKey, consumerGroup);

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

### RedisStreamPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `RedisStreamPublisher<T>(string connectionString, string streamKey)` | String de conexão Redis e a stream key para publicar |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa em JSON e adiciona ao stream como campo `data` |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publica todas as mensagens concorrentemente |
| `DisposeAsync()` | Fecha e libera a conexão Redis |

### RedisStreamConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `RedisStreamConsumer<T>(string connectionString, string streamKey, string consumerGroup, string? consumerName)` | String de conexão Redis, stream key, consumer group e nome do consumer opcional |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Cria o consumer group (se necessário) e inicia o loop de polling |
| `StopAsync(CancellationToken ct)` | Cancela o loop de polling |
| `DisposeAsync()` | Cancela, aguarda conclusão e libera a conexão Redis |

## Requisitos

- .NET 9.0+
- `StackExchange.Redis` (instalado automaticamente)
- Um servidor Redis com suporte a Streams — Redis 5.0+ (ou Docker: `docker run -p 6379:6379 redis`)

## Licença

[Apache-2.0](../../LICENSE)



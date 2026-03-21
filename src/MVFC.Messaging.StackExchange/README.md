# MVFC.Messaging.StackExchange

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/main/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.StackExchange)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.StackExchange)

A .NET messaging provider for **Redis Streams** via [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis), built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `RedisStreamPublisher<T>` and `RedisStreamConsumer<T>` for publishing and consuming JSON-serialized messages using Redis Streams with **consumer groups** and **explicit acknowledgment**.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.StackExchange](https://www.nuget.org/packages/MVFC.Messaging.StackExchange) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.StackExchange) |

## Installation

```sh
dotnet add package MVFC.Messaging.StackExchange
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `StackExchange.Redis`.

## Configuration

### Connection String

Both `RedisStreamPublisher<T>` and `RedisStreamConsumer<T>` accept a Redis **connection string**:

```
localhost:6379
redis-host:6379,password=secret,ssl=true
redis-host:6379,abortConnect=false,connectTimeout=5000
```

### Stream Key and Consumer Groups

The publisher writes to a **stream key** (like `orders-stream`). The consumer reads from the same stream key using a **consumer group** — this enables multiple consumers to share the workload:

- **Stream key:** A unique name for the Redis Stream (e.g. `orders-stream`).
- **Consumer group:** A named group; each message is delivered to only one consumer in the group.
- **Consumer name:** An optional unique identifier for this consumer instance (auto-generated if not provided).

### appsettings.json Example

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

## Usage

### Publishing a Single Message

```csharp
using MVFC.Messaging.StackExchange.Redis;

var connectionString = "localhost:6379";
var streamKey = "orders-stream";

await using var publisher = new RedisStreamPublisher<OrderCreated>(connectionString, streamKey);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

The message is serialized to JSON and added to the stream as a `data` field entry via `StreamAddAsync`.

### Publishing a Batch

Batch publishing sends all messages concurrently using `Task.WhenAll`:

```csharp
var orders = new[]
{
    new OrderCreated(1, "Keyboard", 149.90m),
    new OrderCreated(2, "Mouse", 59.90m),
    new OrderCreated(3, "Monitor", 899.00m)
};

await publisher.PublishBatchAsync(orders);
```

### Consuming Messages

The consumer uses Redis **consumer groups** (`StreamReadGroupAsync`) to read messages from the stream. Each successfully processed message is explicitly acknowledged:

```csharp
using MVFC.Messaging.StackExchange.Redis;

var connectionString = "localhost:6379";
var streamKey = "orders-stream";
var consumerGroup = "order-processors";

await using var consumer = new RedisStreamConsumer<OrderCreated>(connectionString, streamKey, consumerGroup);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- On startup, **creates the consumer group** if it doesn't exist (handles `BUSYGROUP` error gracefully).
- Reads up to **10 messages** per poll using `StreamReadGroupAsync`.
- When no new messages are available, waits **100ms** before polling again.
- Each message's `data` field is deserialized from JSON. Null or empty entries are skipped.
- After successful handler execution, `StreamAcknowledgeAsync` is called — the message is removed from the consumer's pending list.
- Handler exceptions are caught to prevent blocking the consumer loop.
- A unique **consumer name** is auto-generated (UUID) if not provided, allowing multiple instances in the same group.
- `DisposeAsync` cancels the consume loop, waits for completion, and closes the Redis connection.

### Complete Publish + Consume Example

```csharp
using MVFC.Messaging.StackExchange.Redis;

var connectionString = "localhost:6379";
var streamKey = "orders-stream";
var consumerGroup = "order-processors";

await using var publisher = new RedisStreamPublisher<OrderCreated>(connectionString, streamKey);
await using var consumer = new RedisStreamConsumer<OrderCreated>(connectionString, streamKey, consumerGroup);

// Start consuming
var received = new TaskCompletionSource<OrderCreated>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Received: Order #{msg.OrderId} — {msg.Product}");
    received.SetResult(msg);
}, CancellationToken.None);

await Task.Delay(1000); // Wait for consumer initialization

// Publish
await publisher.PublishAsync(new OrderCreated(42, "Keyboard", 149.90m));

// Wait for the message to be consumed
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

// Cleanup
await consumer.StopAsync();
```

## API Reference

### RedisStreamPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `RedisStreamPublisher<T>(string connectionString, string streamKey)` | Redis connection string and the stream key to publish to |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes to JSON and adds to the stream as a `data` field |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publishes all messages concurrently |
| `DisposeAsync()` | Closes and disposes the Redis connection |

### RedisStreamConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `RedisStreamConsumer<T>(string connectionString, string streamKey, string consumerGroup, string? consumerName)` | Redis connection string, stream key, consumer group, and optional consumer name |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Creates the consumer group (if needed) and starts the polling loop |
| `StopAsync(CancellationToken ct)` | Cancels the polling loop |
| `DisposeAsync()` | Cancels, waits for completion, and disposes the Redis connection |

## Requirements

- .NET 9.0+
- `StackExchange.Redis` (installed automatically)
- A running Redis server with Streams support — Redis 5.0+ (or Docker: `docker run -p 6379:6379 redis`)

## License

[Apache-2.0](../../LICENSE)


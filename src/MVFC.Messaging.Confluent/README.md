# MVFC.Messaging.Confluent

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Confluent)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Confluent)

A .NET messaging provider for **Apache Kafka** via the [Confluent.Kafka](https://github.com/confluentinc/confluent-kafka-dotnet) client, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `KafkaPublisher<T>` and `KafkaConsumer<T>` for publishing and consuming JSON-serialized messages in Kafka topics with idempotent production and manual offset commit.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.Confluent](https://www.nuget.org/packages/MVFC.Messaging.Confluent) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Confluent) |

## Installation

```sh
dotnet add package MVFC.Messaging.Confluent
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `Confluent.Kafka`.

## Configuration

### Bootstrap Servers

Both `KafkaPublisher<T>` and `KafkaConsumer<T>` receive the Kafka **bootstrap servers** string — a comma-separated list of `host:port` broker addresses.

```
localhost:9092
broker1:9092,broker2:9092,broker3:9092
```

### Producer Configuration

The publisher is pre-configured with:

| Setting | Value | Description |
|---|---|---|
| `Acks` | `All` | Waits for all in-sync replicas to acknowledge the write |
| `EnableIdempotence` | `true` | Ensures exactly-once delivery semantics (no duplicates) |

### Consumer Configuration

The consumer requires a **group ID** for consumer group management and is pre-configured with:

| Setting | Value | Description |
|---|---|---|
| `AutoOffsetReset` | `Earliest` | Starts reading from the beginning when no committed offset exists |
| `EnableAutoCommit` | `false` | Offsets are committed manually after successful processing |

### appsettings.json Example

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "orders",
    "GroupId": "order-processor"
  }
}
```

```csharp
var servers = builder.Configuration["Kafka:BootstrapServers"]!;
var topic = builder.Configuration["Kafka:Topic"]!;
var groupId = builder.Configuration["Kafka:GroupId"]!;
```

## Usage

### Publishing a Single Message

```csharp
using MVFC.Messaging.Confluent.Kafka;

var bootstrapServers = "localhost:9092";
var topic = "orders";

await using var publisher = new KafkaPublisher<OrderCreated>(bootstrapServers, topic);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

Each message is serialized to JSON and published with a unique **UUID v7 key** (time-ordered) for optimal partition distribution.

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

The consumer starts a background loop that calls `Consume` (blocking), deserializes the message, invokes your handler, and then **manually commits the offset**:

```csharp
using MVFC.Messaging.Confluent.Kafka;

var bootstrapServers = "localhost:9092";
var topic = "orders";
var groupId = "order-processor";

await using var consumer = new KafkaConsumer<OrderCreated>(bootstrapServers, topic, groupId);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Uses the **consumer group** protocol — multiple instances with the same `groupId` share partitions automatically.
- `AutoOffsetReset.Earliest` ensures no messages are missed when a consumer group is first created.
- Offsets are committed **manually** after each successful handler invocation — no data loss on crashes.
- The consume loop catches `OperationCanceledException` for graceful shutdown.
- `DisposeAsync` cancels the loop, waits for completion, then closes and disposes the underlying Kafka consumer.

### Complete Publish + Consume Example

```csharp
using MVFC.Messaging.Confluent.Kafka;

var bootstrapServers = "localhost:9092";
var topic = "orders";
var groupId = "order-processor";

await using var publisher = new KafkaPublisher<OrderCreated>(bootstrapServers, topic);
await using var consumer = new KafkaConsumer<OrderCreated>(bootstrapServers, topic, groupId);

// Start consuming
var received = new TaskCompletionSource<OrderCreated>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Received: Order #{msg.OrderId} — {msg.Product}");
    received.SetResult(msg);
}, CancellationToken.None);

// Publish
await publisher.PublishAsync(new OrderCreated(42, "Keyboard", 149.90m));

// Wait for the message to be consumed
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));

// Cleanup
await consumer.StopAsync();
```

## API Reference

### KafkaPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `KafkaPublisher<T>(string bootstrapServers, string topic)` | Kafka broker addresses and the target topic name |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes the message and produces it to the topic with a UUID v7 key |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Produces all messages concurrently |
| `DisposeAsync()` | Flushes pending messages (10s timeout) and disposes the producer |

### KafkaConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `KafkaConsumer<T>(string bootstrapServers, string topic, string groupId)` | Kafka broker addresses, topic name, and consumer group ID |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Subscribes to the topic and starts the consume loop |
| `StopAsync(CancellationToken ct)` | Cancels the consume loop |
| `DisposeAsync()` | Cancels, waits for completion, closes, and disposes the consumer |

## Requirements

- .NET 9.0+
- `Confluent.Kafka` (installed automatically)
- A running Kafka cluster (or Docker: `docker run -p 9092:9092 confluentinc/cp-kafka`)

## License

[Apache-2.0](../../LICENSE)
# MVFC.Messaging.RabbitMQ

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-10-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.RabbitMQ)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.RabbitMQ)

A .NET messaging provider for **[RabbitMQ](https://www.rabbitmq.com/)**, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `RabbitMqPublisher<T>` and `RabbitMqConsumer<T>` for publishing and consuming JSON-serialized messages in RabbitMQ queues with persistent delivery, QoS prefetch, and explicit acknowledgment.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.RabbitMQ](https://www.nuget.org/packages/MVFC.Messaging.RabbitMQ) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.RabbitMQ) |

## Installation

```sh
dotnet add package MVFC.Messaging.RabbitMQ
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `RabbitMQ.Client`.

## Configuration

### Connection String

Both `RabbitMqPublisher<T>` and `RabbitMqConsumer<T>` use an **async factory pattern** (`CreateAsync`) and accept a **connection string** (AMQP URI) and a **queue name**.

```
amqp://guest:guest@localhost:5672/
amqp://user:password@rabbitmq-host:5672/vhost
```

### Queue Configuration

The queue is **automatically declared** during creation with the following settings:

| Setting | Value | Description |
|---|---|---|
| `durable` | `true` | Queue survives broker restarts |
| `exclusive` | `false` | Queue is not exclusive to the connection |
| `autoDelete` | `false` | Queue is not deleted when the last consumer disconnects |

### Consumer QoS

The consumer sets a **prefetch count of 10**, which means the broker delivers up to 10 unacknowledged messages to the consumer at a time.

### appsettings.json Example

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

## Usage

### Async Factory Pattern

Unlike other providers, RabbitMQ uses an **async factory method** instead of a regular constructor, because establishing the connection and declaring the queue are async operations:

```csharp
// ✅ Correct — use CreateAsync
await using var publisher = await RabbitMqPublisher<OrderCreated>.CreateAsync(connectionString, queueName);
await using var consumer = await RabbitMqConsumer<OrderCreated>.CreateAsync(connectionString, queueName);

// ❌ Wrong — constructor is private
// var publisher = new RabbitMqPublisher<OrderCreated>(...);
```

### Publishing a Single Message

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

var connectionString = "amqp://guest:guest@localhost:5672/";
var queueName = "orders";

await using var publisher = await RabbitMqPublisher<OrderCreated>.CreateAsync(connectionString, queueName);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

Messages are serialized to JSON, encoded as UTF-8, and published with `Persistent = true` to survive broker restarts.

### Publishing a Batch

Batch publishing sends each message sequentially using the same channel:

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

The consumer uses `AsyncEventingBasicConsumer` for event-driven message delivery with explicit acknowledgment:

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

var connectionString = "amqp://guest:guest@localhost:5672/";
var queueName = "orders";

await using var consumer = await RabbitMqConsumer<OrderCreated>.CreateAsync(connectionString, queueName);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Uses `AsyncEventingBasicConsumer` for fully asynchronous message handling.
- `autoAck` is set to **false** — messages must be explicitly acknowledged.
- On successful handler execution, `BasicAckAsync` is called — the message is removed from the queue.
- On handler exception, `BasicNackAsync` is called with `requeue: true` — the message returns to the queue for redelivery.
- QoS prefetch count is **10** — the broker delivers up to 10 messages at a time before waiting for acks.

### Complete Publish + Consume Example

```csharp
using MVFC.Messaging.RabbitMQ.Rabbit;

var connectionString = "amqp://guest:guest@localhost:5672/";
var queueName = "orders";

await using var publisher = await RabbitMqPublisher<OrderCreated>.CreateAsync(connectionString, queueName);
await using var consumer = await RabbitMqConsumer<OrderCreated>.CreateAsync(connectionString, queueName);

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

### RabbitMqPublisher\<T\>

| Factory Method | Parameters |
|---|---|
| `RabbitMqPublisher<T>.CreateAsync(string connectionString, string queueName)` | AMQP URI and queue name (queue is auto-declared) |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes to JSON, encodes as UTF-8, and publishes with persistence |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publishes each message sequentially |
| `DisposeAsync()` | Closes and disposes the channel and connection |

### RabbitMqConsumer\<T\>

| Factory Method | Parameters |
|---|---|
| `RabbitMqConsumer<T>.CreateAsync(string connectionString, string queueName)` | AMQP URI and queue name (queue is auto-declared, QoS is configured) |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Creates an async event consumer and starts consuming |
| `StopAsync(CancellationToken ct)` | No-op (consumer stops when connection is disposed) |
| `DisposeAsync()` | Closes and disposes the channel and connection |

## Requirements

- .NET 10.0+
- `RabbitMQ.Client` (installed automatically)
- A running RabbitMQ server (or Docker: `docker run -p 5672:5672 -p 15672:15672 rabbitmq:management`)

## License

[Apache-2.0](../../LICENSE)
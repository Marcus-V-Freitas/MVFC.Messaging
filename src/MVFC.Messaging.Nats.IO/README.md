# MVFC.Messaging.Nats.IO

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/main/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Nats.IO)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Nats.IO)

A .NET messaging provider for **[NATS](https://nats.io/)**, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `NatsPublisher<T>` and `NatsConsumer<T>` for publishing and consuming JSON-serialized messages on NATS subjects with the official [NATS.Client.Core](https://github.com/nats-io/nats.net) library.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.Nats.IO](https://www.nuget.org/packages/MVFC.Messaging.Nats.IO) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Nats.IO) |

## Installation

```sh
dotnet add package MVFC.Messaging.Nats.IO
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `NATS.Client.Core`.

## Configuration

### NATS Server URL

Both `NatsPublisher<T>` and `NatsConsumer<T>` accept a **URL** and a **subject** name. The URL points to your NATS server:

```
nats://localhost:4222
nats://user:password@nats-server:4222
```

### Subjects

NATS uses a flat, string-based subject hierarchy (e.g. `orders.created`, `orders.>`). You provide the subject to both publisher and consumer — matching subjects allows messages to flow.

### appsettings.json Example

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

## Usage

### Publishing a Single Message

```csharp
using MVFC.Messaging.Nats.IO.Nats;

var url = "nats://localhost:4222";
var subject = "orders.created";

await using var publisher = new NatsPublisher<OrderCreated>(url, subject);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

The message is serialized to JSON and published to the NATS subject.

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

The consumer subscribes to the NATS subject and runs a background loop that yields messages as they arrive:

```csharp
using MVFC.Messaging.Nats.IO.Nats;

var url = "nats://localhost:4222";
var subject = "orders.created";

await using var consumer = new NatsConsumer<OrderCreated>(url, subject);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Uses `NatsConnection.SubscribeAsync<string>` to receive messages as an async stream.
- Messages with null or empty data are skipped.
- Each valid message is deserialized from JSON and passed to the handler.
- `OperationCanceledException` in the handler is re-thrown for proper cancellation propagation; other exceptions are caught to avoid breaking the consume loop.
- `DisposeAsync` cancels the consume loop, waits for completion, and disposes the NATS connection.

### Complete Publish + Consume Example

```csharp
using MVFC.Messaging.Nats.IO.Nats;

var url = "nats://localhost:4222";
var subject = "orders.created";

await using var publisher = new NatsPublisher<OrderCreated>(url, subject);
await using var consumer = new NatsConsumer<OrderCreated>(url, subject);

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
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

// Cleanup
await consumer.StopAsync();
```

## API Reference

### NatsPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `NatsPublisher<T>(string url, string subject)` | NATS server URL and the target subject |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes the message to JSON and publishes to the subject |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publishes all messages concurrently |
| `DisposeAsync()` | Disposes the underlying `NatsConnection` |

### NatsConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `NatsConsumer<T>(string url, string subject)` | NATS server URL and the subject to subscribe to |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Subscribes to the subject and starts the consume loop |
| `StopAsync(CancellationToken ct)` | Cancels the consume loop |
| `DisposeAsync()` | Cancels, waits for completion, and disposes the connection |

## Requirements

- .NET 9.0+
- `NATS.Client.Core` (installed automatically)
- A running NATS server (or Docker: `docker run -p 4222:4222 nats`)

## License

[Apache-2.0](../../LICENSE)


# MVFC.Messaging.Azure

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Azure)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Azure)

A .NET messaging provider for **Azure Service Bus**, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `ServiceBusPublisher<T>` and `ServiceBusConsumer<T>` for publishing and consuming JSON-serialized messages in queues and topics with AMQP/TCP transport.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.Azure](https://www.nuget.org/packages/MVFC.Messaging.Azure) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Azure) |

## Installation

```sh
dotnet add package MVFC.Messaging.Azure
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `Azure.Messaging.ServiceBus`.

## Configuration

### Connection String

Both `ServiceBusPublisher<T>` and `ServiceBusConsumer<T>` accept a **connection string** and a **queue or topic name** in their constructor. You can find the connection string in the Azure Portal under your Service Bus namespace → **Shared access policies**.

```
Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<your-key>
```

### Transport

The provider is configured to use **AMQP over TCP** (`ServiceBusTransportType.AmqpTcp`) for optimal performance. This is a direct TCP connection — no WebSocket overhead.

### appsettings.json Example

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

## Usage

### Publishing a Single Message

```csharp
using MVFC.Messaging.Azure.ServiceBus;

var connectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
var queueName = "orders";

await using var publisher = new ServiceBusPublisher<OrderCreated>(connectionString, queueName);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

The message is serialized to JSON and wrapped in a `ServiceBusMessage`.

### Publishing a Batch

The publisher uses the native `ServiceBusMessageBatch` API for batch operations. If a message cannot fit in the batch (size limit reached), an `InvalidOperationException` is thrown:

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

The consumer uses Azure Service Bus's built-in `ServiceBusProcessor`, which manages message polling, concurrency, and error handling internally. Messages are **not auto-completed** — they are explicitly completed after successful handler execution:

```csharp
using MVFC.Messaging.Azure.ServiceBus;

var connectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
var queueName = "orders";

await using var consumer = new ServiceBusConsumer<OrderCreated>(connectionString, queueName);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Uses the `ServiceBusProcessor` event-driven model — no manual polling needed.
- `AutoCompleteMessages` is set to **false**; messages are completed explicitly after handler success.
- If the handler throws an exception, the message is **not completed** and will be retried based on the queue's max delivery count.
- Error events are logged to `Console.WriteLine` (you can customize this by extending the class).
- `StartAsync` starts the processor; `StopAsync` stops it gracefully.

### Complete Publish + Consume Example

```csharp
using MVFC.Messaging.Azure.ServiceBus;

var connectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
var queueName = "orders";

await using var publisher = new ServiceBusPublisher<OrderCreated>(connectionString, queueName);
await using var consumer = new ServiceBusConsumer<OrderCreated>(connectionString, queueName);

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

### ServiceBusPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `ServiceBusPublisher<T>(string connectionString, string queueOrTopicName)` | Service Bus connection string and queue/topic name |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes the message and sends it to the queue/topic |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Creates a native batch and sends all messages in one operation |
| `DisposeAsync()` | Disposes the `ServiceBusSender` and `ServiceBusClient` |

### ServiceBusConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `ServiceBusConsumer<T>(string connectionString, string queueOrTopicName)` | Service Bus connection string and queue/topic name |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Starts the Service Bus processor |
| `StopAsync(CancellationToken ct)` | Stops the processor gracefully |
| `DisposeAsync()` | Disposes the `ServiceBusProcessor` and `ServiceBusClient` |

## Requirements

- .NET 9.0+
- `Azure.Messaging.ServiceBus` (installed automatically)

## License

[Apache-2.0](../../LICENSE)
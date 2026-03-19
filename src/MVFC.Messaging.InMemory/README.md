# MVFC.Messaging.InMemory

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-10-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.InMemory)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.InMemory)

A .NET messaging provider for **in-memory messaging**, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `InMemoryPublisher<T>` and `InMemoryConsumer<T>` backed by `System.Threading.Channels.Channel<T>` — ideal for **unit testing**, **integration testing**, and **local development** without external infrastructure.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.InMemory](https://www.nuget.org/packages/MVFC.Messaging.InMemory) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.InMemory) |

## Installation

```sh
dotnet add package MVFC.Messaging.InMemory
```

This package depends on `MVFC.Messaging.Core` (installed automatically). No external dependencies.

## Configuration

The InMemory provider requires **no external infrastructure** — no connection strings, no credentials, no brokers. Both the publisher and consumer share a `Channel<T>` instance that acts as the in-memory message queue:

```csharp
using System.Threading.Channels;

// Unbounded channel — no backpressure, accepts messages indefinitely
var channel = Channel.CreateUnbounded<OrderCreated>();

// Bounded channel — limits the buffer to 100 messages (producer blocks when full)
var channel = Channel.CreateBounded<OrderCreated>(100);
```

The publisher and consumer **must share the same `Channel<T>` instance** for messages to flow between them.

## Usage

### Publishing a Single Message

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

var channel = Channel.CreateUnbounded<OrderCreated>();
await using var publisher = new InMemoryPublisher<OrderCreated>(channel);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

Messages are written directly to the channel — no serialization overhead.

### Publishing a Batch

Batch publishing writes each message sequentially to the channel:

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

The consumer reads from the channel using `ReadAllAsync`, which yields messages as they arrive:

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

var channel = Channel.CreateUnbounded<OrderCreated>();
await using var consumer = new InMemoryConsumer<OrderCreated>(channel);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Reads from the channel using `Channel.Reader.ReadAllAsync`, which is a native async stream — no polling or delays.
- Messages are passed directly to the handler (no deserialization needed).
- `StartAsync` launches a background `Task.Run` loop; `StopAsync` cancels it and waits for completion.

### Complete Publish + Consume Example

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

var channel = Channel.CreateUnbounded<OrderCreated>();

await using var publisher = new InMemoryPublisher<OrderCreated>(channel);
await using var consumer = new InMemoryConsumer<OrderCreated>(channel);

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
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

// Cleanup
await consumer.StopAsync();
```

### Using in Unit Tests

The InMemory provider is perfect for testing messaging logic without external dependencies:

```csharp
[Fact]
public async Task Should_Process_Order_When_Published()
{
    // Arrange
    var channel = Channel.CreateUnbounded<OrderCreated>();
    await using var publisher = new InMemoryPublisher<OrderCreated>(channel);
    await using var consumer = new InMemoryConsumer<OrderCreated>(channel);

    var tcs = new TaskCompletionSource<OrderCreated>();
    await consumer.StartAsync(async (msg, ct) => tcs.SetResult(msg), CancellationToken.None);

    // Act
    var order = new OrderCreated(1, "Test Product", 9.99m);
    await publisher.PublishAsync(order);

    // Assert
    var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(1, result.OrderId);
    Assert.Equal("Test Product", result.Product);

    await consumer.StopAsync();
}
```

## API Reference

### InMemoryPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `InMemoryPublisher<T>(Channel<T> channel)` | The shared channel to write messages to |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Writes the message directly to the channel |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Writes each message sequentially to the channel |
| `DisposeAsync()` | No-op (channel is managed externally) |

### InMemoryConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `InMemoryConsumer<T>(Channel<T> channel)` | The shared channel to read messages from |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Starts reading from the channel in a background task |
| `StopAsync(CancellationToken ct)` | Cancels the background task and waits for completion |
| `DisposeAsync()` | Cancels the background task and disposes the CancellationTokenSource |

## Requirements

- .NET 10.0+

## License

[Apache-2.0](../../LICENSE)
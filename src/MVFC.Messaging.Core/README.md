# MVFC.Messaging.Core

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-10-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Core)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Core)

The foundation layer for the MVFC.Messaging ecosystem. This package provides the interfaces and abstract base classes that every messaging provider implements. You typically **do not use this package directly** — instead, you install it alongside a provider package (AWS, Azure, RabbitMQ, etc.) that adds the concrete broker integration.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.Core](https://www.nuget.org/packages/MVFC.Messaging.Core) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Core) |

## Installation

```sh
dotnet add package MVFC.Messaging.Core
```

## Core Interfaces

### IMessagePublisher\<T\>

Defines the contract for publishing messages to a broker. All provider publishers implement this interface.

```csharp
public interface IMessagePublisher<in T>
{
    /// <summary>
    /// Publishes a single message to the broker.
    /// </summary>
    Task PublishAsync(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple messages to the broker in a single operation.
    /// </summary>
    Task PublishBatchAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default);
}
```

**Key points:**
- The generic type `T` is contravariant (`in`), so a `IMessagePublisher<object>` can accept any message type.
- Both methods support `CancellationToken` for cooperative cancellation.
- Batch publishing semantics vary by provider (some use native batch APIs, others publish sequentially).

### IMessageConsumer\<T\>

Defines the contract for consuming messages from a broker. All provider consumers implement this interface.

```csharp
public interface IMessageConsumer<T>
{
    /// <summary>
    /// Starts consuming messages. Each received message is passed to the handler delegate.
    /// </summary>
    Task StartAsync(Func<T, CancellationToken, Task> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops consuming messages gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

**Key points:**
- The `messageHandler` delegate receives each message along with a `CancellationToken`.
- `StartAsync` is non-blocking — it starts a background consumer loop and returns immediately.
- `StopAsync` signals the consumer loop to stop and waits for pending work to complete.

## Base Classes

### MessagePublisherBase\<T\>

Abstract base class that implements `IMessagePublisher<T>` with built-in argument validation. Provider publishers inherit from this class and override only the internal methods:

```csharp
public abstract class MessagePublisherBase<T> : IMessagePublisher<T>
{
    // Override these in your provider:
    protected abstract Task PublishInternalAsync(T message, CancellationToken cancellationToken);
    protected abstract Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken);

    // Public API — validates arguments, then delegates to the internal methods:
    public async Task PublishAsync(T message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await PublishInternalAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishBatchAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        await PublishBatchInternalAsync(messages, cancellationToken).ConfigureAwait(false);
    }
}
```

### MessageConsumerBase\<T\>

Abstract base class that implements `IMessageConsumer<T>` with handler storage and argument validation. Provider consumers inherit from this class:

```csharp
public abstract class MessageConsumerBase<T> : IMessageConsumer<T>
{
    // The stored handler delegate — available to subclasses via the protected property.
    protected Func<T, CancellationToken, Task>? Handler { get; private set; }

    // Override these in your provider:
    protected abstract Task StartInternalAsync(CancellationToken cancellationToken);
    protected abstract Task StopInternalAsync(CancellationToken cancellationToken);

    // Public API — stores the handler, then starts the consumer loop:
    public async Task StartAsync(Func<T, CancellationToken, Task> messageHandler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageHandler);
        Handler = messageHandler;
        await StartInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default) =>
        await StopInternalAsync(cancellationToken).ConfigureAwait(false);
}
```

## Creating a Custom Provider

To create your own messaging provider, you need to:

1. **Inherit from `MessagePublisherBase<T>`** and implement `PublishInternalAsync` and `PublishBatchInternalAsync`.
2. **Inherit from `MessageConsumerBase<T>`** and implement `StartInternalAsync` and `StopInternalAsync`.
3. **Implement `IAsyncDisposable`** on both classes to properly release broker connections.

```csharp
public sealed class MyBrokerPublisher<T>(MyBrokerClient client, string destination)
    : MessagePublisherBase<T>, IAsyncDisposable
{
    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        await client.SendAsync(destination, json, cancellationToken);
    }

    protected override async Task PublishBatchInternalAsync(
        IEnumerable<T> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
            await PublishInternalAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

## Compatible Providers

- [MVFC.Messaging.AWS](../MVFC.Messaging.AWS/README.md) — Amazon SQS
- [MVFC.Messaging.Azure](../MVFC.Messaging.Azure/README.md) — Azure Service Bus
- [MVFC.Messaging.Confluent](../MVFC.Messaging.Confluent/README.md) — Apache Kafka (Confluent)
- [MVFC.Messaging.GCP](../MVFC.Messaging.GCP/README.md) — Google Cloud Pub/Sub
- [MVFC.Messaging.InMemory](../MVFC.Messaging.InMemory/README.md) — In-Memory (for testing)
- [MVFC.Messaging.Nats.IO](../MVFC.Messaging.Nats.IO/README.md) — NATS.io
- [MVFC.Messaging.RabbitMQ](../MVFC.Messaging.RabbitMQ/README.md) — RabbitMQ
- [MVFC.Messaging.StackExchange](../MVFC.Messaging.StackExchange/README.md) — Redis Streams

## Requirements

- .NET 10.0+

## License

[Apache-2.0](../../LICENSE)
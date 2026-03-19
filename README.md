# MVFC.Messaging

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)

A complete .NET library suite for asynchronous messaging, with a pluggable, extensible architecture that lets you publish and consume messages across multiple brokers using a single, unified API.

## Motivation

Working with message brokers in .NET often means:

- Tightly coupling your code to a specific broker SDK (SQS, Service Bus, Kafka, etc.).
- Rewriting publish/consume logic every time you switch or add a broker.
- Duplicating serialization, error handling, and lifecycle management across projects.
- Making it hard to unit-test message flows without spinning up real infrastructure.

**MVFC.Messaging** solves this by providing a thin abstraction layer — two interfaces (`IMessagePublisher<T>` and `IMessageConsumer<T>`) and two base classes — that every provider implements. You pick the provider for your broker, instantiate it, and get a consistent, async-first API for publishing, consuming, and disposing resources. Swapping brokers requires changing only the provider instantiation; your business logic stays the same.

## Architecture

All packages follow the same pattern:

- `IMessagePublisher<T>` — contract for publishing single and batch messages.
- `IMessageConsumer<T>` — contract for starting/stopping message consumption with a handler delegate.
- `MessagePublisherBase<T>` — base class that adds argument validation and delegates to `PublishInternalAsync` / `PublishBatchInternalAsync`.
- `MessageConsumerBase<T>` — base class that stores the handler and delegates to `StartInternalAsync` / `StopInternalAsync`.
- Each provider package (AWS, Azure, Confluent, etc.) implements the `*InternalAsync` methods using the broker's native SDK.

Once you understand one provider, all others work identically.

## Packages

| Package | Broker | Downloads |
|---|---|---|
| [MVFC.Messaging.Core](src/MVFC.Messaging.Core/README.md) | Base abstractions | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Core) |
| [MVFC.Messaging.AWS](src/MVFC.Messaging.AWS/README.md) | Amazon SQS | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.AWS) |
| [MVFC.Messaging.Azure](src/MVFC.Messaging.Azure/README.md) | Azure Service Bus | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Azure) |
| [MVFC.Messaging.Confluent](src/MVFC.Messaging.Confluent/README.md) | Apache Kafka (Confluent) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Confluent) |
| [MVFC.Messaging.GCP](src/MVFC.Messaging.GCP/README.md) | Google Cloud Pub/Sub | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.GCP) |
| [MVFC.Messaging.InMemory](src/MVFC.Messaging.InMemory/README.md) | In-Memory (for testing) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.InMemory) |
| [MVFC.Messaging.Nats.IO](src/MVFC.Messaging.Nats.IO/README.md) | NATS.io | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Nats.IO) |
| [MVFC.Messaging.RabbitMQ](src/MVFC.Messaging.RabbitMQ/README.md) | RabbitMQ | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.RabbitMQ) |
| [MVFC.Messaging.StackExchange](src/MVFC.Messaging.StackExchange/README.md) | Redis Streams | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.StackExchange) |

***

## Installation

Install the core package and the provider for your target broker:

```sh
# Base abstractions (always required)
dotnet add package MVFC.Messaging.Core

# Pick your provider
dotnet add package MVFC.Messaging.AWS            # Amazon SQS
dotnet add package MVFC.Messaging.Azure          # Azure Service Bus
dotnet add package MVFC.Messaging.Confluent      # Apache Kafka
dotnet add package MVFC.Messaging.GCP            # Google Pub/Sub
dotnet add package MVFC.Messaging.InMemory       # In-Memory (testing)
dotnet add package MVFC.Messaging.Nats.IO        # NATS.io
dotnet add package MVFC.Messaging.RabbitMQ       # RabbitMQ
dotnet add package MVFC.Messaging.StackExchange  # Redis Streams
```

## Quick Start

The examples below use the **InMemory** provider, which requires no external infrastructure — perfect for getting started. Every other provider follows the exact same pattern; only the constructor arguments change.

### 1. Define your message type

```csharp
public record OrderCreated(int OrderId, string Product, decimal Total);
```

### 2. Create a publisher and consumer

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

// The in-memory provider uses a System.Threading.Channels.Channel<T> as its backing store.
var channel = Channel.CreateUnbounded<OrderCreated>();

await using var publisher = new InMemoryPublisher<OrderCreated>(channel);
await using var consumer  = new InMemoryConsumer<OrderCreated>(channel);
```

### 3. Start consuming

```csharp
await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Order #{message.OrderId}: {message.Product} — ${message.Total}");
}, cancellationToken);
```

### 4. Publish a message

```csharp
var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order, cancellationToken);
```

### 5. Publish a batch

```csharp
var orders = new[]
{
    new OrderCreated(2, "Mouse", 59.90m),
    new OrderCreated(3, "Monitor", 899.00m),
    new OrderCreated(4, "Headset", 199.90m)
};

await publisher.PublishBatchAsync(orders, cancellationToken);
```

### 6. Stop consuming

```csharp
await consumer.StopAsync(cancellationToken);
```

## Available API

All methods are defined by `IMessagePublisher<T>` / `IMessageConsumer<T>` and available in every provider.

### Publisher Methods

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Publishes a single message to the broker |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publishes multiple messages in a single operation |

### Consumer Methods

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Starts consuming messages, invoking `handler` for each one |
| `StopAsync(CancellationToken ct)` | Stops consuming messages gracefully |

### Resource Disposal

All providers implement `IAsyncDisposable`. Always use `await using` or call `DisposeAsync()` to release connections and background tasks properly.

## Provider Overview

| Provider | Transport | Constructor (Publisher) | Constructor (Consumer) |
|---|---|---|---|
| **InMemory** | `Channel<T>` | `Channel<T>` | `Channel<T>` |
| **AWS** | Amazon SQS | `IAmazonSQS`, `queueUrl` | `IAmazonSQS`, `queueUrl` |
| **Azure** | Service Bus | `connectionString`, `queueOrTopicName` | `connectionString`, `queueOrTopicName` |
| **Confluent** | Apache Kafka | `bootstrapServers`, `topic` | `bootstrapServers`, `topic`, `groupId` |
| **GCP** | Google Pub/Sub | `projectId`, `topicId` | `projectId`, `subscriptionId` |
| **Nats.IO** | NATS | `url`, `subject` | `url`, `subject` |
| **RabbitMQ** | RabbitMQ | `CreateAsync(connectionString, queueName)` | `CreateAsync(connectionString, queueName)` |
| **StackExchange** | Redis Streams | `connectionString`, `streamKey` | `connectionString`, `streamKey`, `consumerGroup` |

> See each provider's README for detailed configuration and complete usage examples.

## Project Structure

```text
src/
  MVFC.Messaging.Core/           # Base abstractions (interfaces + base classes)
  MVFC.Messaging.AWS/            # Amazon SQS provider
  MVFC.Messaging.Azure/          # Azure Service Bus provider
  MVFC.Messaging.Confluent/      # Apache Kafka (Confluent) provider
  MVFC.Messaging.GCP/            # Google Cloud Pub/Sub provider
  MVFC.Messaging.InMemory/       # In-Memory provider (for testing)
  MVFC.Messaging.Nats.IO/        # NATS.io provider
  MVFC.Messaging.RabbitMQ/       # RabbitMQ provider
  MVFC.Messaging.StackExchange/  # Redis Streams provider
tests/
  MVFC.Messaging.Tests/          # Unit and integration tests
tools/
  # Build tooling (Cake)
```

## Requirements

- .NET 9.0+
- The underlying broker SDK for each provider (pulled automatically via NuGet)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[Apache-2.0](LICENSE)

# MVFC.Messaging.GCP

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/main/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.GCP)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.GCP)

A .NET messaging provider for **Google Cloud Pub/Sub**, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `PubSubPublisher<T>` and `PubSubConsumer<T>` for publishing and consuming JSON-serialized messages using the official Google Cloud client library with built-in emulator support.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.GCP](https://www.nuget.org/packages/MVFC.Messaging.GCP) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.GCP) |

## Installation

```sh
dotnet add package MVFC.Messaging.GCP
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `Google.Cloud.PubSub.V1`.

## Configuration

### Authentication

Google Cloud client libraries use [Application Default Credentials (ADC)](https://cloud.google.com/docs/authentication/application-default-credentials). Common setups:

| Method | Description |
|---|---|
| **`gcloud auth application-default login`** | For local development — stores credentials in `~/.config/gcloud/` |
| **Service Account JSON** | Set `GOOGLE_APPLICATION_CREDENTIALS` env var to the JSON key file path |
| **Workload Identity** | On GKE, Cloud Run, or Compute Engine — automatic credential discovery |
| **Emulator** | Set `PUBSUB_EMULATOR_HOST=localhost:8085` for local testing |

### Emulator Detection

Both `PubSubPublisher<T>` and `PubSubConsumer<T>` are configured with `EmulatorDetection.EmulatorOrProduction`. This means:
- If `PUBSUB_EMULATOR_HOST` is set → connects to the emulator.
- Otherwise → connects to production Google Cloud Pub/Sub.

No code changes needed between local and production environments.

### Constructor Parameters

- **Publisher:** `projectId` (your GCP project) and `topicId` (the Pub/Sub topic name).
- **Consumer:** `projectId` and `subscriptionId` (the Pub/Sub subscription name).

### appsettings.json Example

```json
{
  "GCP": {
    "ProjectId": "my-gcp-project",
    "TopicId": "orders",
    "SubscriptionId": "orders-subscription"
  }
}
```

```csharp
var projectId = builder.Configuration["GCP:ProjectId"]!;
var topicId = builder.Configuration["GCP:TopicId"]!;
var subscriptionId = builder.Configuration["GCP:SubscriptionId"]!;
```

## Usage

### Publishing a Single Message

```csharp
using MVFC.Messaging.GCP.PubSub;

var projectId = "my-gcp-project";
var topicId = "orders";

await using var publisher = new PubSubPublisher<OrderCreated>(projectId, topicId);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

The message is serialized to JSON and published as the Pub/Sub message data.

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

The consumer wraps Google Cloud's `SubscriberClient`, which handles pull-based message delivery with automatic flow control. Each message receives an **Ack** (success) or **Nack** (failure) reply:

```csharp
using MVFC.Messaging.GCP.PubSub;

var projectId = "my-gcp-project";
var subscriptionId = "orders-subscription";

await using var consumer = new PubSubConsumer<OrderCreated>(projectId, subscriptionId);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Uses Google Cloud's `SubscriberClient.StartAsync` for managed message delivery.
- Successful handler execution results in an **Ack** — the message is removed from the subscription.
- If the handler throws an exception, a **Nack** is sent — the message will be redelivered.
- A 1-second initialization delay ensures the subscriber is fully ready before returning.
- `StopAsync` gracefully stops the subscriber and waits for all pending handlers to complete.

### Complete Publish + Consume Example

```csharp
using MVFC.Messaging.GCP.PubSub;

// Using emulator: set PUBSUB_EMULATOR_HOST=localhost:8085
var projectId = "my-gcp-project";
var topicId = "orders";
var subscriptionId = "orders-subscription";

await using var publisher = new PubSubPublisher<OrderCreated>(projectId, topicId);
await using var consumer = new PubSubConsumer<OrderCreated>(projectId, subscriptionId);

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

### PubSubPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `PubSubPublisher<T>(string projectId, string topicId)` | GCP project ID and the Pub/Sub topic name |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes the message to JSON and publishes to the topic |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publishes all messages concurrently |
| `DisposeAsync()` | Shuts down the underlying `PublisherClient` |

### PubSubConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `PubSubConsumer<T>(string projectId, string subscriptionId)` | GCP project ID and the Pub/Sub subscription name |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Starts the subscriber with Ack/Nack handling |
| `StopAsync(CancellationToken ct)` | Stops the subscriber and waits for pending handlers |
| `DisposeAsync()` | Stops the subscriber and ignores any shutdown exceptions |

## Requirements

- .NET 9.0+
- `Google.Cloud.PubSub.V1` (installed automatically)
- Google Cloud credentials (ADC) or Pub/Sub emulator

## License

[Apache-2.0](../../LICENSE)


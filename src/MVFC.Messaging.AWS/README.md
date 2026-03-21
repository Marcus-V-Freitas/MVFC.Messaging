# MVFC.Messaging.AWS

> 🇧🇷 [Leia em Português](README.pt-br.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/main/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.AWS)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.AWS)

A .NET messaging provider for **Amazon Simple Queue Service (SQS)**, built on top of [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.md). Provides `SqsPublisher<T>` and `SqsConsumer<T>` for publishing and consuming JSON-serialized messages in SQS queues with a clean, async-first API.

## Package

| Package | Downloads |
|---|---|
| [MVFC.Messaging.AWS](https://www.nuget.org/packages/MVFC.Messaging.AWS) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.AWS) |

## Installation

```sh
dotnet add package MVFC.Messaging.AWS
```

This package depends on `MVFC.Messaging.Core` (installed automatically) and `AWSSDK.SQS`.

## Configuration

### AWS Credentials

The `SqsPublisher<T>` and `SqsConsumer<T>` both receive an `IAmazonSQS` client instance, so you control how credentials are provided. Common approaches:

| Method | Description |
|---|---|
| **Environment variables** | Set `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and optionally `AWS_SESSION_TOKEN` |
| **Shared credentials file** | `~/.aws/credentials` with a `[default]` profile |
| **IAM Role** | When running on EC2, ECS, or Lambda, the SDK automatically uses the instance/task role |
| **LocalStack** | For local development, point `ServiceURL` to `http://localhost:4566` |

### Creating the SQS Client

```csharp
using Amazon.SQS;

// Production — uses the default credential chain (env vars, IAM role, etc.)
var sqsClient = new AmazonSQSClient();

// Local development with LocalStack
var sqsClient = new AmazonSQSClient(new AmazonSQSConfig
{
    ServiceURL = "http://localhost:4566"
});
```

### Queue URL

Both `SqsPublisher<T>` and `SqsConsumer<T>` require the **queue URL** (not the queue name). You can obtain it by creating a queue or calling `GetQueueUrlAsync`:

```csharp
var createResponse = await sqsClient.CreateQueueAsync("my-queue");
var queueUrl = createResponse.QueueUrl;
// e.g. "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue"
```

## Usage

### Publishing a Single Message

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

var sqsClient = new AmazonSQSClient();
var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue";

await using var publisher = new SqsPublisher<OrderCreated>(sqsClient, queueUrl);

var order = new OrderCreated(1, "Keyboard", 149.90m);
await publisher.PublishAsync(order);
```

The message is serialized to JSON using `System.Text.Json` and sent as the SQS message body.

### Publishing a Batch

SQS supports sending up to 10 messages in a single `SendMessageBatch` API call. The `PublishBatchAsync` method uses this native batch API for optimal performance:

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

The consumer runs a background polling loop that calls `ReceiveMessage` with long polling (5-second wait), processes each message through your handler, and automatically deletes the message from the queue after successful processing:

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

var sqsClient = new AmazonSQSClient();
var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue";

await using var consumer = new SqsConsumer<OrderCreated>(sqsClient, queueUrl);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processing order #{message.OrderId}: {message.Product}");
    // Your business logic here
}, cancellationToken);

// ... later, when shutting down:
await consumer.StopAsync();
```

**Consumer behavior:**
- Polls for up to **10 messages** per request (SQS maximum).
- Uses **long polling** with a 5-second wait time to reduce empty responses.
- After successful handler execution, the message is **automatically deleted** from the queue.
- If the handler throws an exception, the message is **not deleted** and becomes visible again after the queue's visibility timeout.
- The polling loop runs until `StopAsync` is called or the `CancellationToken` is cancelled.

### Complete Publish + Consume Example

```csharp
using Amazon.SQS;
using MVFC.Messaging.AWS.SQS;

// Setup
var sqsClient = new AmazonSQSClient(new AmazonSQSConfig { ServiceURL = "http://localhost:4566" });
var createResponse = await sqsClient.CreateQueueAsync($"orders-{Guid.NewGuid()}");
var queueUrl = createResponse.QueueUrl;

await using var publisher = new SqsPublisher<OrderCreated>(sqsClient, queueUrl);
await using var consumer = new SqsConsumer<OrderCreated>(sqsClient, queueUrl);

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
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));

// Cleanup
await consumer.StopAsync();
```

## API Reference

### SqsPublisher\<T\>

| Constructor | Parameters |
|---|---|
| `SqsPublisher<T>(IAmazonSQS sqsClient, string queueUrl)` | The SQS client instance and the queue URL to publish to |

| Method | Description |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializes the message to JSON and sends it to the SQS queue |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Sends up to 10 messages in a single SQS batch request |
| `DisposeAsync()` | Disposes the underlying `IAmazonSQS` client |

### SqsConsumer\<T\>

| Constructor | Parameters |
|---|---|
| `SqsConsumer<T>(IAmazonSQS sqsClient, string queueUrl)` | The SQS client instance and the queue URL to consume from |

| Method | Description |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Starts the background polling loop |
| `StopAsync(CancellationToken ct)` | Cancels the polling loop and waits for completion |
| `DisposeAsync()` | Disposes the underlying `IAmazonSQS` client |

## Requirements

- .NET 9.0+
- `AWSSDK.SQS` (installed automatically)

## License

[Apache-2.0](../../LICENSE)


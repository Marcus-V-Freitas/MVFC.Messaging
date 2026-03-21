# MVFC.Messaging.Confluent

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/main/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Confluent)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Confluent)

Provedor de mensageria .NET para **Apache Kafka** via cliente [Confluent.Kafka](https://github.com/confluentinc/confluent-kafka-dotnet), construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `KafkaPublisher<T>` e `KafkaConsumer<T>` para publicação e consumo de mensagens serializadas em JSON em tópicos Kafka com produção idempotente e commit manual de offset.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.Confluent](https://www.nuget.org/packages/MVFC.Messaging.Confluent) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Confluent) |

## Instalação

```sh
dotnet add package MVFC.Messaging.Confluent
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente) e `Confluent.Kafka`.

## Configuração

### Bootstrap Servers

Tanto o `KafkaPublisher<T>` quanto o `KafkaConsumer<T>` recebem a string de **bootstrap servers** do Kafka — uma lista separada por vírgula de endereços `host:porta` dos brokers.

```
localhost:9092
broker1:9092,broker2:9092,broker3:9092
```

### Configuração do Producer

O publisher é pré-configurado com:

| Configuração | Valor | Descrição |
|---|---|---|
| `Acks` | `All` | Aguarda que todas as réplicas in-sync confirmem a escrita |
| `EnableIdempotence` | `true` | Garante semântica de entrega exactly-once (sem duplicatas) |

### Configuração do Consumer

O consumer requer um **group ID** para gerenciamento de grupo de consumidores e é pré-configurado com:

| Configuração | Valor | Descrição |
|---|---|---|
| `AutoOffsetReset` | `Earliest` | Inicia a leitura do começo quando não existe offset commitado |
| `EnableAutoCommit` | `false` | Offsets são commitados manualmente após processamento bem-sucedido |

### Exemplo appsettings.json

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

## Uso

### Publicando uma Única Mensagem

```csharp
using MVFC.Messaging.Confluent.Kafka;

var bootstrapServers = "localhost:9092";
var topic = "orders";

await using var publisher = new KafkaPublisher<OrderCreated>(bootstrapServers, topic);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

Cada mensagem é serializada em JSON e publicada com uma **chave UUID v7** única (ordenada por tempo) para distribuição otimizada entre partições.

### Publicando um Lote

A publicação em lote envia todas as mensagens concorrentemente usando `Task.WhenAll`:

```csharp
var orders = new[]
{
    new OrderCreated(1, "Teclado", 149.90m),
    new OrderCreated(2, "Mouse", 59.90m),
    new OrderCreated(3, "Monitor", 899.00m)
};

await publisher.PublishBatchAsync(orders);
```

### Consumindo Mensagens

O consumer inicia um loop em background que chama `Consume` (bloqueante), deserializa a mensagem, invoca seu handler e então **commita o offset manualmente**:

```csharp
using MVFC.Messaging.Confluent.Kafka;

var bootstrapServers = "localhost:9092";
var topic = "orders";
var groupId = "order-processor";

await using var consumer = new KafkaConsumer<OrderCreated>(bootstrapServers, topic, groupId);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Usa o protocolo de **grupo de consumidores** — múltiplas instâncias com o mesmo `groupId` compartilham partições automaticamente.
- `AutoOffsetReset.Earliest` garante que nenhuma mensagem seja perdida quando um grupo de consumidores é criado pela primeira vez.
- Offsets são commitados **manualmente** após cada invocação bem-sucedida do handler — sem perda de dados em crashes.
- O loop de consumo captura `OperationCanceledException` para shutdown graceful.
- `DisposeAsync` cancela o loop, aguarda a conclusão, depois fecha e libera o consumer Kafka subjacente.

### Exemplo Completo de Publicação + Consumo

```csharp
using MVFC.Messaging.Confluent.Kafka;

var bootstrapServers = "localhost:9092";
var topic = "orders";
var groupId = "order-processor";

await using var publisher = new KafkaPublisher<OrderCreated>(bootstrapServers, topic);
await using var consumer = new KafkaConsumer<OrderCreated>(bootstrapServers, topic, groupId);

// Inicia consumo
var received = new TaskCompletionSource<OrderCreated>();
await consumer.StartAsync(async (msg, ct) =>
{
    Console.WriteLine($"Recebido: Pedido #{msg.OrderId} — {msg.Product}");
    received.SetResult(msg);
}, CancellationToken.None);

// Publica
await publisher.PublishAsync(new OrderCreated(42, "Teclado", 149.90m));

// Aguarda a mensagem ser consumida
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));

// Limpeza
await consumer.StopAsync();
```

## Referência da API

### KafkaPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `KafkaPublisher<T>(string bootstrapServers, string topic)` | Endereços dos brokers Kafka e nome do tópico alvo |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Serializa a mensagem e produz no tópico com uma chave UUID v7 |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Produz todas as mensagens concorrentemente |
| `DisposeAsync()` | Faz flush das mensagens pendentes (timeout 10s) e libera o produtor |

### KafkaConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `KafkaConsumer<T>(string bootstrapServers, string topic, string groupId)` | Endereços dos brokers Kafka, nome do tópico e ID do grupo de consumidores |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Inscreve-se no tópico e inicia o loop de consumo |
| `StopAsync(CancellationToken ct)` | Cancela o loop de consumo |
| `DisposeAsync()` | Cancela, aguarda conclusão, fecha e libera o consumer |

## Requisitos

- .NET 9.0+
- `Confluent.Kafka` (instalado automaticamente)
- Um cluster Kafka em execução (ou Docker: `docker run -p 9092:9092 confluentinc/cp-kafka`)

## Licença

[Apache-2.0](../../LICENSE)



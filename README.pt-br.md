# MVFC.Messaging

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)

Suíte completa de bibliotecas .NET para mensageria assíncrona, com uma arquitetura plugável e extensível que permite publicar e consumir mensagens em múltiplos brokers através de uma API unificada.

## Motivação

Trabalhar com message brokers em .NET frequentemente significa:

- Acoplar fortemente seu código a um SDK específico de broker (SQS, Service Bus, Kafka, etc.).
- Reescrever lógica de publicação/consumo toda vez que você troca ou adiciona um broker.
- Duplicar serialização, tratamento de erros e gerenciamento de ciclo de vida entre projetos.
- Dificultar testes unitários de fluxos de mensagens sem subir infraestrutura real.

**MVFC.Messaging** resolve isso fornecendo uma camada fina de abstração — duas interfaces (`IMessagePublisher<T>` e `IMessageConsumer<T>`) e duas classes base — que cada provedor implementa. Você escolhe o provedor do seu broker, instancia-o e obtém uma API consistente, async-first, para publicar, consumir e liberar recursos. Trocar de broker exige apenas mudar a instanciação do provedor; sua lógica de negócio permanece a mesma.

## Arquitetura

Todos os pacotes seguem o mesmo padrão:

- `IMessagePublisher<T>` — contrato para publicação de mensagens individuais e em lote.
- `IMessageConsumer<T>` — contrato para iniciar/parar o consumo de mensagens com um delegate handler.
- `MessagePublisherBase<T>` — classe base que adiciona validação de argumentos e delega para `PublishInternalAsync` / `PublishBatchInternalAsync`.
- `MessageConsumerBase<T>` — classe base que armazena o handler e delega para `StartInternalAsync` / `StopInternalAsync`.
- Cada pacote provedor (AWS, Azure, Confluent, etc.) implementa os métodos `*InternalAsync` usando o SDK nativo do broker.

Uma vez que você entende um provedor, todos os outros funcionam de forma idêntica.

## Pacotes

| Pacote | Broker | Downloads |
|---|---|---|
| [MVFC.Messaging.Core](src/MVFC.Messaging.Core/README.pt-br.md) | Abstrações base | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Core) |
| [MVFC.Messaging.AWS](src/MVFC.Messaging.AWS/README.pt-br.md) | Amazon SQS | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.AWS) |
| [MVFC.Messaging.Azure](src/MVFC.Messaging.Azure/README.pt-br.md) | Azure Service Bus | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Azure) |
| [MVFC.Messaging.Confluent](src/MVFC.Messaging.Confluent/README.pt-br.md) | Apache Kafka (Confluent) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Confluent) |
| [MVFC.Messaging.GCP](src/MVFC.Messaging.GCP/README.pt-br.md) | Google Cloud Pub/Sub | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.GCP) |
| [MVFC.Messaging.InMemory](src/MVFC.Messaging.InMemory/README.pt-br.md) | In-Memory (para testes) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.InMemory) |
| [MVFC.Messaging.Nats.IO](src/MVFC.Messaging.Nats.IO/README.pt-br.md) | NATS.io | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Nats.IO) |
| [MVFC.Messaging.RabbitMQ](src/MVFC.Messaging.RabbitMQ/README.pt-br.md) | RabbitMQ | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.RabbitMQ) |
| [MVFC.Messaging.StackExchange](src/MVFC.Messaging.StackExchange/README.pt-br.md) | Redis Streams | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.StackExchange) |

***

## Instalação

Instale o pacote base e o provedor do seu broker alvo:

```sh
# Abstrações base (sempre necessário)
dotnet add package MVFC.Messaging.Core

# Escolha seu provedor
dotnet add package MVFC.Messaging.AWS            # Amazon SQS
dotnet add package MVFC.Messaging.Azure          # Azure Service Bus
dotnet add package MVFC.Messaging.Confluent      # Apache Kafka
dotnet add package MVFC.Messaging.GCP            # Google Pub/Sub
dotnet add package MVFC.Messaging.InMemory       # In-Memory (testes)
dotnet add package MVFC.Messaging.Nats.IO        # NATS.io
dotnet add package MVFC.Messaging.RabbitMQ       # RabbitMQ
dotnet add package MVFC.Messaging.StackExchange  # Redis Streams
```

## Início Rápido

Os exemplos abaixo usam o provedor **InMemory**, que não requer infraestrutura externa — perfeito para começar. Todos os outros provedores seguem exatamente o mesmo padrão; apenas os argumentos do construtor mudam.

### 1. Defina seu tipo de mensagem

```csharp
public record OrderCreated(int OrderId, string Product, decimal Total);
```

### 2. Crie um publisher e consumer

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

// O provedor in-memory usa um System.Threading.Channels.Channel<T> como armazenamento.
var channel = Channel.CreateUnbounded<OrderCreated>();

await using var publisher = new InMemoryPublisher<OrderCreated>(channel);
await using var consumer  = new InMemoryConsumer<OrderCreated>(channel);
```

### 3. Inicie o consumo

```csharp
await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Pedido #{message.OrderId}: {message.Product} — R${message.Total}");
}, cancellationToken);
```

### 4. Publique uma mensagem

```csharp
var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order, cancellationToken);
```

### 5. Publique um lote

```csharp
var orders = new[]
{
    new OrderCreated(2, "Mouse", 59.90m),
    new OrderCreated(3, "Monitor", 899.00m),
    new OrderCreated(4, "Headset", 199.90m)
};

await publisher.PublishBatchAsync(orders, cancellationToken);
```

### 6. Pare o consumo

```csharp
await consumer.StopAsync(cancellationToken);
```

## API Disponível

Todos os métodos são definidos por `IMessagePublisher<T>` / `IMessageConsumer<T>` e estão disponíveis em todos os provedores.

### Métodos do Publisher

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Publica uma única mensagem no broker |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Publica múltiplas mensagens em uma única operação |

### Métodos do Consumer

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Inicia o consumo de mensagens, invocando `handler` para cada uma |
| `StopAsync(CancellationToken ct)` | Para o consumo de mensagens de forma graceful |

### Liberação de Recursos

Todos os provedores implementam `IAsyncDisposable`. Sempre use `await using` ou chame `DisposeAsync()` para liberar conexões e tarefas em segundo plano corretamente.

## Visão Geral dos Provedores

| Provedor | Transporte | Construtor (Publisher) | Construtor (Consumer) |
|---|---|---|---|
| **InMemory** | `Channel<T>` | `Channel<T>` | `Channel<T>` |
| **AWS** | Amazon SQS | `IAmazonSQS`, `queueUrl` | `IAmazonSQS`, `queueUrl` |
| **Azure** | Service Bus | `connectionString`, `queueOrTopicName` | `connectionString`, `queueOrTopicName` |
| **Confluent** | Apache Kafka | `bootstrapServers`, `topic` | `bootstrapServers`, `topic`, `groupId` |
| **GCP** | Google Pub/Sub | `projectId`, `topicId` | `projectId`, `subscriptionId` |
| **Nats.IO** | NATS | `url`, `subject` | `url`, `subject` |
| **RabbitMQ** | RabbitMQ | `CreateAsync(connectionString, queueName)` | `CreateAsync(connectionString, queueName)` |
| **StackExchange** | Redis Streams | `connectionString`, `streamKey` | `connectionString`, `streamKey`, `consumerGroup` |

> Consulte o README de cada provedor para configuração detalhada e exemplos completos de uso.

## Estrutura do Projeto

```text
src/
  MVFC.Messaging.Core/           # Abstrações base (interfaces + classes base)
  MVFC.Messaging.AWS/            # Provedor Amazon SQS
  MVFC.Messaging.Azure/          # Provedor Azure Service Bus
  MVFC.Messaging.Confluent/      # Provedor Apache Kafka (Confluent)
  MVFC.Messaging.GCP/            # Provedor Google Cloud Pub/Sub
  MVFC.Messaging.InMemory/       # Provedor In-Memory (para testes)
  MVFC.Messaging.Nats.IO/        # Provedor NATS.io
  MVFC.Messaging.RabbitMQ/       # Provedor RabbitMQ
  MVFC.Messaging.StackExchange/  # Provedor Redis Streams
tests/
  MVFC.Messaging.Tests/          # Testes unitários e de integração
tools/
  # Ferramentas de build (Cake)
```

## Requisitos

- .NET 9.0+
- O SDK nativo do broker para cada provedor (baixado automaticamente via NuGet)

## Contribuindo

Veja [CONTRIBUTING.md](CONTRIBUTING.md).

## Licença

[Apache-2.0](LICENSE)

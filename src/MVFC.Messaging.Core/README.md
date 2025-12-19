# MVFC.Messaging.Core

Camada base para mensageria, fornecendo interfaces e classes abstratas para implementação de publishers e consumers em diferentes brokers.

## Instalação

Adicione o pacote via NuGet:

```sh
dotnet add package MVFC.Messaging.Core
```

## Interfaces Principais

### IMessagePublisher

Interface para publicação de mensagens.

```csharp
public interface IMessagePublisher : IAsyncDisposable
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
    Task PublishBatchAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default);
}
```

### IMessageConsumer

Interface para consumo de mensagens.

```csharp
public interface IMessageConsumer : IAsyncDisposable
{
    Task StartAsync<T>(Func<T, CancellationToken, Task> onMessage, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

## Classes Base

- **MessagePublisherBase**: Implementação base para publishers.
- **MessageConsumerBase**: Implementação base para consumers.

Essas classes facilitam a criação de novos providers de mensageria, padronizando o ciclo de vida e o contrato de publicação/consumo.

## Como Usar

Você normalmente não utiliza MVFC.Messaging.Core diretamente, mas sim através de um dos providers abaixo, que implementam as interfaces e herdam as classes base deste projeto.

## Providers Compatíveis

- [MVFC.Messaging.AWS](../MVFC.Messaging.AWS/README.md) — Amazon SQS
- [MVFC.Messaging.Azure](../MVFC.Messaging.Azure/README.md) — Azure Service Bus
- [MVFC.Messaging.Confluent](../MVFC.Messaging.Confluent/README.md) — Apache Kafka (Confluent)
- [MVFC.Messaging.GCP](../MVFC.Messaging.GCP/README.md) — Google Pub/Sub
- [MVFC.Messaging.InMemory](../MVFC.Messaging.InMemory/README.md) — In-memory (para testes)
- [MVFC.Messaging.Nats.IO](../MVFC.Messaging.Nats.IO/README.md) — NATS.io
- [MVFC.Messaging.RabbitMQ](../MVFC.Messaging.RabbitMQ/README.md) — RabbitMQ
- [MVFC.Messaging.StackExchange](../MVFC.Messaging.StackExchange/README.md) — Redis Streams

## Exemplos

Veja exemplos de uso nos READMEs dos providers acima.
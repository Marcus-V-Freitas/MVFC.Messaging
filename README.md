# MVFC.Messaging

MVFC.Messaging é um conjunto de bibliotecas para mensageria assíncrona, com uma arquitetura extensível baseada em interfaces e classes base comuns. O projeto permite publicar e consumir mensagens de forma padronizada em diversos brokers, facilitando a troca de implementação sem alterar o código de negócio.

## Principais Características

- **Abstração**: Interfaces e classes base em MVFC.Messaging.Core para padronizar publishers e consumers.
- **Extensível**: Suporte a múltiplos brokers via providers plugáveis.
- **Fácil de usar**: APIs modernas e assíncronas.
- **Testável**: Provider in-memory para testes unitários e integração.

## Providers Disponíveis

Cada provider possui um README próprio com exemplos detalhados de uso e configuração:

- **[MVFC.Messaging.AWS](./MVFC.Messaging.AWS/README.md)** — Amazon SQS
- **[MVFC.Messaging.Azure](./MVFC.Messaging.Azure/README.md)** — Azure Service Bus
- **[MVFC.Messaging.Confluent](./MVFC.Messaging.Confluent/README.md)** — Apache Kafka (Confluent)
- **[MVFC.Messaging.GCP](./MVFC.Messaging.GCP/README.md)** — Google Pub/Sub
- **[MVFC.Messaging.InMemory](./MVFC.Messaging.InMemory/README.md)** — In-memory (para testes)
- **[MVFC.Messaging.Nats.IO](./MVFC.Messaging.Nats.IO/README.md)** — NATS.io
- **[MVFC.Messaging.RabbitMQ](./MVFC.Messaging.RabbitMQ/README.md)** — RabbitMQ
- **[MVFC.Messaging.StackExchange](./MVFC.Messaging.StackExchange/README.md)** — Redis Streams

## Estrutura do Projeto

- **MVFC.Messaging.Core**: Interfaces (`IMessagePublisher`, `IMessageConsumer`) e classes base para abstração.
- **MVFC.Messaging.[Provider]**: Implementações específicas para cada broker.
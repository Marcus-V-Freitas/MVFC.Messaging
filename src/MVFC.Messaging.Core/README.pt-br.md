# MVFC.Messaging.Core

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.Core)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Core)

Camada de fundação do ecossistema MVFC.Messaging. Este pacote fornece as interfaces e classes base abstratas que cada provedor de mensageria implementa. Tipicamente, **você não usa este pacote diretamente** — em vez disso, instala-o junto com um pacote provedor (AWS, Azure, RabbitMQ, etc.) que adiciona a integração concreta com o broker.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.Core](https://www.nuget.org/packages/MVFC.Messaging.Core) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.Core) |

## Instalação

```sh
dotnet add package MVFC.Messaging.Core
```

## Interfaces Principais

### IMessagePublisher\<T\>

Define o contrato para publicação de mensagens em um broker. Todos os publishers dos provedores implementam esta interface.

```csharp
public interface IMessagePublisher<in T>
{
    /// <summary>
    /// Publica uma única mensagem no broker.
    /// </summary>
    Task PublishAsync(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publica múltiplas mensagens no broker em uma única operação.
    /// </summary>
    Task PublishBatchAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default);
}
```

**Pontos-chave:**
- O tipo genérico `T` é contravariante (`in`), então um `IMessagePublisher<object>` pode aceitar qualquer tipo de mensagem.
- Ambos os métodos suportam `CancellationToken` para cancelamento cooperativo.
- A semântica de publicação em lote varia por provedor (alguns usam APIs nativas de batch, outros publicam sequencialmente).

### IMessageConsumer\<T\>

Define o contrato para consumo de mensagens de um broker. Todos os consumers dos provedores implementam esta interface.

```csharp
public interface IMessageConsumer<T>
{
    /// <summary>
    /// Inicia o consumo de mensagens. Cada mensagem recebida é passada para o delegate handler.
    /// </summary>
    Task StartAsync(Func<T, CancellationToken, Task> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Para o consumo de mensagens de forma graceful.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

**Pontos-chave:**
- O delegate `messageHandler` recebe cada mensagem junto com um `CancellationToken`.
- `StartAsync` é não-bloqueante — inicia um loop de consumo em background e retorna imediatamente.
- `StopAsync` sinaliza o loop de consumo para parar e aguarda o trabalho pendente completar.

## Classes Base

### MessagePublisherBase\<T\>

Classe base abstrata que implementa `IMessagePublisher<T>` com validação de argumentos embutida. Os publishers dos provedores herdam desta classe e sobrescrevem apenas os métodos internos:

```csharp
public abstract class MessagePublisherBase<T> : IMessagePublisher<T>
{
    // Sobrescreva estes no seu provedor:
    protected abstract Task PublishInternalAsync(T message, CancellationToken cancellationToken);
    protected abstract Task PublishBatchInternalAsync(IEnumerable<T> messages, CancellationToken cancellationToken);

    // API pública — valida argumentos, depois delega para os métodos internos:
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

Classe base abstrata que implementa `IMessageConsumer<T>` com armazenamento do handler e validação de argumentos. Os consumers dos provedores herdam desta classe:

```csharp
public abstract class MessageConsumerBase<T> : IMessageConsumer<T>
{
    // O delegate handler armazenado — disponível para subclasses via propriedade protegida.
    protected Func<T, CancellationToken, Task>? Handler { get; private set; }

    // Sobrescreva estes no seu provedor:
    protected abstract Task StartInternalAsync(CancellationToken cancellationToken);
    protected abstract Task StopInternalAsync(CancellationToken cancellationToken);

    // API pública — armazena o handler, depois inicia o loop de consumo:
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

## Criando um Provedor Personalizado

Para criar seu próprio provedor de mensageria, você precisa:

1. **Herdar de `MessagePublisherBase<T>`** e implementar `PublishInternalAsync` e `PublishBatchInternalAsync`.
2. **Herdar de `MessageConsumerBase<T>`** e implementar `StartInternalAsync` e `StopInternalAsync`.
3. **Implementar `IAsyncDisposable`** em ambas as classes para liberar corretamente as conexões do broker.

```csharp
public sealed class MeuBrokerPublisher<T>(MeuBrokerClient client, string destino)
    : MessagePublisherBase<T>, IAsyncDisposable
{
    protected override async Task PublishInternalAsync(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        await client.SendAsync(destino, json, cancellationToken);
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

## Provedores Compatíveis

- [MVFC.Messaging.AWS](../MVFC.Messaging.AWS/README.pt-br.md) — Amazon SQS
- [MVFC.Messaging.Azure](../MVFC.Messaging.Azure/README.pt-br.md) — Azure Service Bus
- [MVFC.Messaging.Confluent](../MVFC.Messaging.Confluent/README.pt-br.md) — Apache Kafka (Confluent)
- [MVFC.Messaging.GCP](../MVFC.Messaging.GCP/README.pt-br.md) — Google Cloud Pub/Sub
- [MVFC.Messaging.InMemory](../MVFC.Messaging.InMemory/README.pt-br.md) — In-Memory (para testes)
- [MVFC.Messaging.Nats.IO](../MVFC.Messaging.Nats.IO/README.pt-br.md) — NATS.io
- [MVFC.Messaging.RabbitMQ](../MVFC.Messaging.RabbitMQ/README.pt-br.md) — RabbitMQ
- [MVFC.Messaging.StackExchange](../MVFC.Messaging.StackExchange/README.pt-br.md) — Redis Streams

## Requisitos

- .NET 9.0+

## Licença

[Apache-2.0](../../LICENSE)

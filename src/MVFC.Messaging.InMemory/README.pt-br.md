# MVFC.Messaging.InMemory

> 🇺🇸 [Read in English](README.md)

[![CI](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcus-V-Freitas/MVFC.Messaging/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging/branch/master/graph/badge.svg)](https://codecov.io/gh/Marcus-V-Freitas/MVFC.Messaging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
![Platform](https://img.shields.io/badge/.NET-9%20%7C%2010-blue)
![NuGet Version](https://img.shields.io/nuget/v/MVFC.Messaging.InMemory)
![NuGet Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.InMemory)

Provedor de mensageria .NET para **mensageria in-memory**, construído sobre o [MVFC.Messaging.Core](../MVFC.Messaging.Core/README.pt-br.md). Fornece `InMemoryPublisher<T>` e `InMemoryConsumer<T>` apoiados por `System.Threading.Channels.Channel<T>` — ideal para **testes unitários**, **testes de integração** e **desenvolvimento local** sem infraestrutura externa.

## Pacote

| Pacote | Downloads |
|---|---|
| [MVFC.Messaging.InMemory](https://www.nuget.org/packages/MVFC.Messaging.InMemory) | ![Downloads](https://img.shields.io/nuget/dt/MVFC.Messaging.InMemory) |

## Instalação

```sh
dotnet add package MVFC.Messaging.InMemory
```

Este pacote depende de `MVFC.Messaging.Core` (instalado automaticamente). Sem dependências externas.

## Configuração

O provedor InMemory não requer **nenhuma infraestrutura externa** — sem strings de conexão, sem credenciais, sem brokers. Tanto o publisher quanto o consumer compartilham uma instância de `Channel<T>` que atua como a fila de mensagens in-memory:

```csharp
using System.Threading.Channels;

// Canal ilimitado — sem backpressure, aceita mensagens indefinidamente
var channel = Channel.CreateUnbounded<OrderCreated>();

// Canal limitado — limita o buffer a 100 mensagens (produtor bloqueia quando cheio)
var channel = Channel.CreateBounded<OrderCreated>(100);
```

O publisher e o consumer **devem compartilhar a mesma instância de `Channel<T>`** para que as mensagens fluam entre eles.

## Uso

### Publicando uma Única Mensagem

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

var channel = Channel.CreateUnbounded<OrderCreated>();
await using var publisher = new InMemoryPublisher<OrderCreated>(channel);

var order = new OrderCreated(1, "Teclado", 149.90m);
await publisher.PublishAsync(order);
```

As mensagens são escritas diretamente no canal — sem overhead de serialização.

### Publicando um Lote

A publicação em lote escreve cada mensagem sequencialmente no canal:

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

O consumer lê do canal usando `ReadAllAsync`, que produz mensagens conforme elas chegam:

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

var channel = Channel.CreateUnbounded<OrderCreated>();
await using var consumer = new InMemoryConsumer<OrderCreated>(channel);

await consumer.StartAsync(async (message, ct) =>
{
    Console.WriteLine($"Processando pedido #{message.OrderId}: {message.Product}");
    // Sua lógica de negócio aqui
}, cancellationToken);

// ... depois, ao encerrar:
await consumer.StopAsync();
```

**Comportamento do consumer:**
- Lê do canal usando `Channel.Reader.ReadAllAsync`, que é um async stream nativo — sem polling ou delays.
- Mensagens são passadas diretamente ao handler (sem deserialização necessária).
- `StartAsync` inicia um loop `Task.Run` em background; `StopAsync` o cancela e aguarda a conclusão.

### Exemplo Completo de Publicação + Consumo

```csharp
using System.Threading.Channels;
using MVFC.Messaging.InMemory.Memory;

var channel = Channel.CreateUnbounded<OrderCreated>();

await using var publisher = new InMemoryPublisher<OrderCreated>(channel);
await using var consumer = new InMemoryConsumer<OrderCreated>(channel);

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
var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

// Limpeza
await consumer.StopAsync();
```

### Usando em Testes Unitários

O provedor InMemory é perfeito para testar lógica de mensageria sem dependências externas:

```csharp
[Fact]
public async Task Deve_Processar_Pedido_Quando_Publicado()
{
    // Arrange
    var channel = Channel.CreateUnbounded<OrderCreated>();
    await using var publisher = new InMemoryPublisher<OrderCreated>(channel);
    await using var consumer = new InMemoryConsumer<OrderCreated>(channel);

    var tcs = new TaskCompletionSource<OrderCreated>();
    await consumer.StartAsync(async (msg, ct) => tcs.SetResult(msg), CancellationToken.None);

    // Act
    var order = new OrderCreated(1, "Produto Teste", 9.99m);
    await publisher.PublishAsync(order);

    // Assert
    var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(1, result.OrderId);
    Assert.Equal("Produto Teste", result.Product);

    await consumer.StopAsync();
}
```

## Referência da API

### InMemoryPublisher\<T\>

| Construtor | Parâmetros |
|---|---|
| `InMemoryPublisher<T>(Channel<T> channel)` | O canal compartilhado para escrever mensagens |

| Método | Descrição |
|---|---|
| `PublishAsync(T message, CancellationToken ct)` | Escreve a mensagem diretamente no canal |
| `PublishBatchAsync(IEnumerable<T> messages, CancellationToken ct)` | Escreve cada mensagem sequencialmente no canal |
| `DisposeAsync()` | No-op (o canal é gerenciado externamente) |

### InMemoryConsumer\<T\>

| Construtor | Parâmetros |
|---|---|
| `InMemoryConsumer<T>(Channel<T> channel)` | O canal compartilhado para ler mensagens |

| Método | Descrição |
|---|---|
| `StartAsync(Func<T, CancellationToken, Task> handler, CancellationToken ct)` | Inicia a leitura do canal em uma task em background |
| `StopAsync(CancellationToken ct)` | Cancela a task em background e aguarda a conclusão |
| `DisposeAsync()` | Cancela a task em background e libera o CancellationTokenSource |

## Requisitos

- .NET 9.0+

## Licença

[Apache-2.0](../../LICENSE)

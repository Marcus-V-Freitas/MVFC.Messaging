namespace MVFC.Messaging.Tests.Models;

public sealed record TestMessage
{
    public int Id { get; init; }
    public string Content { get; init; } = string.Empty;
}
namespace InboxOutbox.Options;

public sealed class ConsumerOptions
{
    public required string GroupId { get; init; }
}
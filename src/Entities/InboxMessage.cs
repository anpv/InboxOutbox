using InboxOutbox.Enums;

namespace InboxOutbox.Entities;

public class InboxMessage
{
    public long Id { get; set; }

    public required string Topic { get; init; }

    public required int Partition { get; init; }

    public required long Offset { get; init; }

    public required byte[]? Key { get; init; }

    public required byte[]? Value { get; init; }

    public required IReadOnlyDictionary<string, string?>? Headers { get; init; }

    public InboxMessageStatus Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
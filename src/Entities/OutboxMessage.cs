using InboxOutbox.Enums;

namespace InboxOutbox.Entities;

public class OutboxMessage
{
    public long Id { get; set; }

    public required string Topic { get; init; }

    public required byte[]? Key { get; init; }

    public required byte[]? Value { get; init; }

    public required IReadOnlyDictionary<string, string?>? Headers { get; init; }

    public OutboxMessageStatus Status { get; init; }

    public required Guid InstanceId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
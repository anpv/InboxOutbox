namespace InboxOutbox.EntityFrameworkCore;

public sealed class CreateRangePartitionOperation : CreatePartitionOperation
{
    public required string FromSql { get; init; }

    public required string ToSql { get; init; }
}
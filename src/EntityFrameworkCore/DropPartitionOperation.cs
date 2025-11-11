using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InboxOutbox.EntityFrameworkCore;

public sealed class DropPartitionOperation : MigrationOperation
{
    public override bool IsDestructiveChange => true;

    public required string Name { get; init; }

    public required string? Schema { get; init; }
}
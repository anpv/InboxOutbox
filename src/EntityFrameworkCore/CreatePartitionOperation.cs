using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InboxOutbox.EntityFrameworkCore;

public abstract class CreatePartitionOperation : MigrationOperation
{
    public required string Name { get; init; }

    public required string? Schema { get; init; }

    public required string PartitionOfName { get; init; }

    public required string? PartitionOfSchema { get; init; }
}
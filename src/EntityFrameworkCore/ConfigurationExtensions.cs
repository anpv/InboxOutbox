using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace InboxOutbox.EntityFrameworkCore;

public static class ConfigurationExtensions
{
    public static EntityTypeBuilder<TEntity> HasPartitionByRange<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string keyColumnName)
        where TEntity : class
    {
        return builder.HasAnnotation(ExtNpgsqlAnnotationNames.PartitionByRange.Key, keyColumnName);
    }

    public static OperationBuilder<CreateRangePartitionOperation> CreateRangePartition(
        this MigrationBuilder builder,
        string name,
        string? schema,
        string partitionOfName,
        string? partitionOfSchema,
        string fromSql,
        string toSql)
    {
        var operation = new CreateRangePartitionOperation
        {
            Name = name,
            Schema = schema,
            PartitionOfName = partitionOfName,
            PartitionOfSchema = partitionOfSchema,
            FromSql = fromSql,
            ToSql = toSql,
        };

        builder.Operations.Add(operation);

        return new OperationBuilder<CreateRangePartitionOperation>(operation);
    }

    public static OperationBuilder<DropPartitionOperation> DropPartition(
        this MigrationBuilder builder,
        string name,
        string? schema)
    {
        var operation = new DropPartitionOperation
        {
            Name = name,
            Schema = schema,
        };

        builder.Operations.Add(operation);

        return new OperationBuilder<DropPartitionOperation>(operation);
    }

    public static DbContextOptionsBuilder UseExtNpgsql(this DbContextOptionsBuilder builder)
    {
        return builder
            .ReplaceService<IMigrationsSqlGenerator, ExtNpgsqlMigrationsSqlGenerator>()
            .ReplaceService<IRelationalAnnotationProvider, ExtNpgsqlAnnotationProvider>()
            ;
    }
}
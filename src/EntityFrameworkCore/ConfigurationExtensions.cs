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

    extension(MigrationBuilder builder)
    {
        public OperationBuilder<CreateRangePartitionOperation> CreateRangePartition(
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

        public void CreateMonthlyPartitions(
            string name,
            string? schema,
            int fromYear,
            int fromMonth,
            int numberOfMonths)
        {
            for (var i = 0; i < numberOfMonths; i++)
            {
                var date = new DateTime(fromYear, fromMonth, 1).AddMonths(i);
                
                builder.CreateRangePartition(
                    $"{name}_{date:yyyyMM}",
                    schema,
                    name,
                    schema,
                    $"'{date:yyyy-MM-dd}'",
                    $"'{date.AddMonths(1):yyyy-MM-dd}'");
            }
        }

        public OperationBuilder<DropPartitionOperation> DropPartition(string name, string? schema)
        {
            var operation = new DropPartitionOperation
            {
                Name = name,
                Schema = schema,
            };

            builder.Operations.Add(operation);

            return new OperationBuilder<DropPartitionOperation>(operation);
        }

        public void DropMonthlyPartitions(
            string name,
            string? schema,
            int fromYear,
            int fromMonth,
            int numberOfMonths)
        {
            for (var i = numberOfMonths - 1; i >= 0; i--)
            {
                var date = new DateTime(fromYear, fromMonth, 1).AddMonths(i);

                builder.DropPartition($"{name}_{date:yyyyMM}", schema);
            }
        }
    }

    public static DbContextOptionsBuilder UseExtNpgsql(this DbContextOptionsBuilder builder)
    {
        return builder
            .ReplaceService<IMigrationsSqlGenerator, ExtNpgsqlMigrationsSqlGenerator>()
            .ReplaceService<IRelationalAnnotationProvider, ExtNpgsqlAnnotationProvider>()
            ;
    }
}
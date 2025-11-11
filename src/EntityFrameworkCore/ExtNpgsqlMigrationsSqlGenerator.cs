using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace InboxOutbox.EntityFrameworkCore;

#pragma warning disable EF1001

public sealed class ExtNpgsqlMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
    protected override void Generate(
        CreateTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        base.Generate(operation, model, builder, terminate: false);

        if (operation[ExtNpgsqlAnnotationNames.PartitionByRange.Key] is string keyColumnName)
        {
            builder
                .Append(" PARTITION BY RANGE (")
                .Append(DelimitIdentifier(keyColumnName))
                .AppendLine(");");
        }

        if (terminate)
        {
            EndStatement(builder);
        }
    }

    protected override void Generate(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (operation is CreateRangePartitionOperation createRangePartitionOperation)
        {
            Generate(createRangePartitionOperation, builder);
        }
        else if (operation is DropPartitionOperation dropPartitionOperation)
        {
            Generate(dropPartitionOperation, builder);
        }
        else
        {
            base.Generate(operation, model, builder);
        }
    }

    private void Generate(CreateRangePartitionOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("CREATE TABLE ")
            .Append(DelimitIdentifier(operation.Name, operation.Schema))
            .Append(" PARTITION OF ")
            .Append(DelimitIdentifier(operation.PartitionOfName, operation.PartitionOfSchema))
            .Append(" FOR VALUES FROM (")
            .Append(operation.FromSql)
            .Append(") TO (")
            .Append(operation.ToSql)
            .AppendLine(");");

        EndStatement(builder);
    }

    private void Generate(DropPartitionOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("DROP TABLE ")
            .Append(DelimitIdentifier(operation.Name, operation.Schema))
            .AppendLine(";");

        EndStatement(builder);
    }

    private string DelimitIdentifier(string identifier)
        => Dependencies.SqlGenerationHelper.DelimitIdentifier(identifier);

    private string DelimitIdentifier(string name, string? schema)
        => Dependencies.SqlGenerationHelper.DelimitIdentifier(name, schema);
}
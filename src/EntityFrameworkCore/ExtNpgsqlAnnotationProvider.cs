using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.Internal;

namespace InboxOutbox.EntityFrameworkCore;

#pragma warning disable EF1001

public sealed class ExtNpgsqlAnnotationProvider(RelationalAnnotationProviderDependencies dependencies)
    : NpgsqlAnnotationProvider(dependencies)
{
    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        return base.For(table, designTime).Concat(GetExtAnnotations(table, designTime));
    }

    private static IEnumerable<IAnnotation> GetExtAnnotations(ITable table, bool designTime)
    {
        if (!designTime)
        {
            yield break;
        }

        var entityType = table.EntityTypeMappings.First().TypeBase;

        foreach (var annotation in entityType.GetAnnotations())
        {
            if (annotation.Name.StartsWith(ExtNpgsqlAnnotationNames.BasePrefix, StringComparison.Ordinal))
            {
                yield return annotation;
            }
        }
    }
}
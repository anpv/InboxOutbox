using System.Text.Json;
using InboxOutbox.Entities;
using InboxOutbox.EntityFrameworkCore;
using InboxOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InboxOutbox.EntityConfigurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder
            .ToTable("outbox")
            .HasPartitionByRange("created_at");

        builder
            .HasNoKey(); // Table has no primary key, as id column is not listed in partitioning key.

        builder
            .Property(x => x.Id)
            .UseIdentityAlwaysColumn(); // Rely on GENERATED ALWAYS AS IDENTITY to generate unique IDs.

        builder
            .Property(x => x.Topic)
            .HasMaxLength(255);

        builder
            .Property(x => x.Headers)
            .HasColumnType("jsonb")
            .HasConversion(
                x => x != null
                    ? JsonSerializer.Serialize(x, (JsonSerializerOptions?)null)
                    : null,
                x => x != null
                    ? JsonSerializer.Deserialize<Dictionary<string, string?>>(x, (JsonSerializerOptions?)null)
                    : null,
                ValueComparer.CreateDefault<IReadOnlyDictionary<string, string?>>(false));

        builder
            .Property(x => x.Status)
            .HasSentinel(OutboxMessageStatus.Pending)
            .HasDefaultValue(OutboxMessageStatus.Pending);

        builder
            .Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder
            .HasIndex(x => x.Id) // Filtered to reduce index size
            .HasFilter($"status in ({OutboxMessageStatus.Pending:D}, {OutboxMessageStatus.Sending:D})")
            .HasDatabaseName("ix__outbox__id");
    }
}
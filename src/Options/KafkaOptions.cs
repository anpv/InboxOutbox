namespace InboxOutbox.Options;

public sealed class KafkaOptions
{
    public const string ConfigSectionPath = "Kafka";

    public required string BootstrapServers { get; init; }

    public required bool IsTransactional { get; init; }

    public required string? TransactionalId { get; init; }
}
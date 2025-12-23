namespace InboxOutbox.Options;

public sealed class KafkaOptions
{
    public const string ConfigSectionPath = "Kafka";

    public required string BootstrapServers { get; init; }

    public required ProducerOptions Producer { get; init; }

    public required ConsumerOptions Consumer { get; init; }
}
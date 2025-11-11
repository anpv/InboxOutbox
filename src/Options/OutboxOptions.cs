namespace InboxOutbox.Options;

public sealed class OutboxOptions
{
    public const string ConfigSectionPath = "Kafka:Outbox";

    public required bool IsEnabled { get; init; }

    public required int BatchSize { get; init; }

    public required TimeSpan EmptyDelay { get; init; }

    public required TimeSpan ErrorDelay { get; init; }
}
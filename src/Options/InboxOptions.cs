namespace InboxOutbox.Options;

public sealed class InboxOptions
{
    public const string ConfigSectionPath = "Kafka:Inbox";
    
    public required bool IsEnabled { get; init; }

    public required int BatchSize { get; init; }

    public required TimeSpan BatchTimeout { get; init; }
    
    public required TimeSpan EmptyDelay { get; init; }

    public required TimeSpan ErrorDelay { get; init; }
}
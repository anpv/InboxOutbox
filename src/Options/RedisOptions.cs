namespace InboxOutbox.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public required string Configuration { get; init; }

    public required string InstanceName { get; init; }
}
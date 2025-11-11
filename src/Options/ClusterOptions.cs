using System.ComponentModel.DataAnnotations;

namespace InboxOutbox.Options;

public sealed class ClusterOptions : IValidatableObject
{
    public const string ConfigSectionKey = "Cluster";

    public required bool IsKeepAliveEnabled { get; init; }

    public required TimeSpan KeepAliveInterval { get; init; }

    public required TimeSpan KeepAliveTimeout { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KeepAliveTimeout <= KeepAliveInterval)
        {
            yield return new ValidationResult("Keepalive interval must be less than timeout");
        }
    }
}
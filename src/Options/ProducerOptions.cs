namespace InboxOutbox.Options;

public sealed class ProducerOptions
{
    public required string? TransactionalId { get; init; }

    public bool IsTransactional => TransactionalId is not null;
}
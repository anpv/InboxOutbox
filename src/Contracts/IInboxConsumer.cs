using InboxOutbox.Entities;

namespace InboxOutbox.Contracts;

public interface IInboxConsumer
{
    Task ConsumeAsync(InboxMessage message, CancellationToken token);
}
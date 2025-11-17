using System.Transactions;

namespace InboxOutbox.Implementations;

public sealed class TransactionScopeFactory
{
    public TransactionScope Create()
    {
        return new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(1)
            },
            TransactionScopeAsyncFlowOption.Enabled);
    }
}
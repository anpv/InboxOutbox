using InboxOutbox.Entities;
using InboxOutbox.Enums;
using LinqToDB;
using LinqToDB.Data;

namespace InboxOutbox.Implementations;

public sealed class InboxStorage(IDataContext dataContext)
{
    private static readonly BulkCopyOptions BulkOptions = new(BulkCopyType: BulkCopyType.ProviderSpecific);

    public async Task AddRangeAsync(IEnumerable<InboxMessage> messages, CancellationToken token)
    {
        await dataContext.GetTable<InboxMessage>()
            .BulkCopyAsync(BulkOptions, messages, token);
    }

    public async Task<IReadOnlyCollection<InboxMessage>> GetPendingAsync(
        string topic,
        int partition,
        int count,
        CancellationToken token)
    {
        return await GetActualQuery()
            .Where(x => x.Status == InboxMessageStatus.Pending)
            .Where(x => x.Topic == topic && x.Partition == partition)
            .OrderBy(x => x.Id)
            .Take(count)
            .ToListAsync(token);
    }

    public async Task MarkAsReceivedAsync(long messageId, CancellationToken token)
    {
        await GetActualQuery()
            .Where(x => x.Status == InboxMessageStatus.Pending)
            .Where(x => x.Id == messageId)
            .Set(x => x.Status, InboxMessageStatus.Received)
            .Set(x => x.UpdatedAt, () => Sql.CurrentTzTimestamp)
            .UpdateAsync(token);
    }

    public async Task MarkAsFailedAsync(long messageId, CancellationToken token)
    {
        await GetActualQuery()
            .Where(x => x.Status == InboxMessageStatus.Pending)
            .Where(x => x.Id == messageId)
            .Set(x => x.Status, InboxMessageStatus.Failed)
            .Set(x => x.UpdatedAt, () => Sql.CurrentTzTimestamp)
            .UpdateAsync(token);
    }

    private IQueryable<InboxMessage> GetActualQuery()
    {
        // To use minimal number of query partitions
        return dataContext.GetTable<InboxMessage>()
            .Where(x => x.CreatedAt.Between(Sql.CurrentTzTimestamp.AddDays(-3),Sql.CurrentTzTimestamp));
    }
}
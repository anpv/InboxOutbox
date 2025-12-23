using InboxOutbox.Entities;
using InboxOutbox.Enums;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;

namespace InboxOutbox.Implementations;

public sealed class OutboxStorage(IDataContext dataContext)
{
    private static readonly BulkCopyOptions BulkOptions = new(BulkCopyType: BulkCopyType.ProviderSpecific);

    public async Task AddAsync(OutboxMessage message, CancellationToken token)
    {
        await dataContext.InsertAsync(message, token: token);
    }

    public async Task AddRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken token)
    {
        await dataContext.GetTable<OutboxMessage>()
            .BulkCopyAsync(BulkOptions, messages, token);
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> GetSendingAsync(int count, CancellationToken token)
    {
        return await GetActualQuery()
            .Where(x => x.Status == OutboxMessageStatus.Sending)
            .OrderBy(x => x.Id)
            .Take(count)
            .ToListAsync(token);
    }

    public async Task MarkAsPendingAsync(IEnumerable<long> messageIds, CancellationToken token)
    {
        await GetActualQuery()
            .Where(x => x.Status == OutboxMessageStatus.Sending) // To use filtered index
            .Where(x => messageIds.Contains(x.Id))
            .Set(x => x.Status, OutboxMessageStatus.Pending)
            .Set(x => x.UpdatedAt, () => Sql.CurrentTzTimestamp)
            .UpdateAsync(token);
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> MarkAsSendingAsync(
        int count,
        Guid instanceId,
        CancellationToken token)
    {
        var query = GetActualQuery()
            .Where(x => x.Status == OutboxMessageStatus.Pending); // To use filtered index

        var messageIds = query
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(count)
            .AsPostgreSQL()
            .ForUpdateSkipLockedHint();

        return await query
            .Where(x => messageIds.Contains(x.Id))
            .Set(x => x.Status, OutboxMessageStatus.Sending)
            .Set(x => x.UpdatedAt, () => Sql.CurrentTzTimestamp)
            .Set(x => x.InstanceId, instanceId)
            .UpdateWithOutputAsync((deleted, inserted) => inserted, token: token);
    }

    public async Task MarkAsSentAsync(IEnumerable<long> messageIds, CancellationToken token)
    {
        await GetActualQuery()
            .Where(x => x.Status == OutboxMessageStatus.Sending) // To use filtered index
            .Where(x => messageIds.Contains(x.Id))
            .Set(x => x.Status, OutboxMessageStatus.Sent)
            .Set(x => x.UpdatedAt, () => Sql.CurrentTzTimestamp)
            .UpdateAsync(token);
    }

    public IQueryable<OutboxMessage> GetActualQuery()
    {
        // To use minimal number of query partitions and avoid infinite processing on fail
        return dataContext.GetTable<OutboxMessage>()
            .Where(x => x.CreatedAt.Between(
                Sql.CurrentTzTimestamp.AddDays(-1),
                Sql.CurrentTzTimestamp));
    }
}
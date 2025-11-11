using InboxOutbox.Contracts;
using InboxOutbox.Entities;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;

namespace InboxOutbox.Implementations;

public sealed class OutboxProcessor(
    OutboxStorage storage,
    RawKafkaProducer producer,
    IClusterService clusterService,
    IOptions<OutboxOptions> options)
{
    public async Task<bool> ProcessAsync(CancellationToken token)
    {
        var messages = await storage.MarkAsSendingAsync(
            options.Value.BatchSize,
            clusterService.CurrentInstanceId,
            token);

        if (messages.Count > 0)
        {
            await SendAsync(messages, token);
        }

        return messages.Count == options.Value.BatchSize;
    }

    public async Task<bool> ProcessInStuckAsync(CancellationToken token)
    {
        var messages = await storage.GetSendingAsync(options.Value.BatchSize, token);

        if (messages.Count > 0)
        {
            await ChangeStatusesAsync(messages, token);
        }

        return messages.Count == options.Value.BatchSize;
    }

    private async Task SendAsync(IReadOnlyCollection<OutboxMessage> messages, CancellationToken token)
    {
        var messageIds = messages.Select(x => x.Id).ToArray();

        try
        {
            producer.BeginTransaction();
            var tasks = messages.Select(x => producer.ProduceAsync(x.Topic, x.Key, x.Value, x.Headers, token));
            await Task.WhenAll(tasks);
            await storage.MarkAsSentAsync(messageIds, token);
            producer.CommitTransaction();
        }
        catch
        {
            producer.AbortTransaction();

            throw;
        }
    }

    private async Task ChangeStatusesAsync(IReadOnlyCollection<OutboxMessage> messages, CancellationToken token)
    {
        var stuckMessageIds = new List<long>();

        foreach (var message in messages)
        {
            if (!await clusterService.IsAliveAsync(message.InstanceId, token))
            {
                stuckMessageIds.Add(message.Id);
            }
        }

        await storage.MarkAsPendingAsync(stuckMessageIds, token);
    }
}
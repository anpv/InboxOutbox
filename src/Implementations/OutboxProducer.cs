using InboxOutbox.Contracts;
using InboxOutbox.Entities;

namespace InboxOutbox.Implementations;

internal sealed class OutboxProducer<TKey, TValue>(
    string topic,
    OutboxStorage storage,
    TimeProvider timeProvider,
    IClusterService clusterService,
    IKafkaSerializer serializer)
    : IKafkaProducer<TKey, TValue>
{
    public async Task ProduceAsync(
        TKey key,
        TValue value,
        IReadOnlyDictionary<string, string?>? headers,
        CancellationToken token)
    {
        var message = CreateMessage(key, value, headers);

        await storage.AddAsync(message, token);
    }

    public async Task ProduceAsync(IEnumerable<ProducerRecord<TKey, TValue>> records, CancellationToken token)
    {
        var messages = records.Select(x => CreateMessage(x.Key, x.Value, x.Headers));

        await storage.AddRangeAsync(messages, token);
    }

    private OutboxMessage CreateMessage(TKey key, TValue value, IReadOnlyDictionary<string, string?>? headers) =>
        new()
        {
            Topic = topic,
            Key = serializer.SerializeKey(key),
            Value = serializer.SerializeValue(value),
            Headers = headers,
            InstanceId = clusterService.CurrentInstanceId,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
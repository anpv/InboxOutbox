using InboxOutbox.Contracts;
using InboxOutbox.Entities;

namespace InboxOutbox.Implementations;

public sealed class InboxConsumer<TKey, TValue>(
    IKafkaConsumer<TKey, TValue> consumer,
    IKafkaDeserializer<TKey> keyDeserializer,
    IKafkaDeserializer<TValue> valueDeserializer)
    : IInboxConsumer
{
    public async Task ConsumeAsync(InboxMessage message, CancellationToken token)
    {
        var consumeResult = new KafkaConsumeResult<TKey, TValue>(message, keyDeserializer, valueDeserializer);
        await consumer.ConsumeAsync(consumeResult, token);
    }
}
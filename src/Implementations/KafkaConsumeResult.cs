using Confluent.Kafka;
using InboxOutbox.Contracts;
using InboxOutbox.Entities;
using InboxOutbox.Extensions;

namespace InboxOutbox.Implementations;

public sealed class KafkaConsumeResult<TKey, TValue> : IConsumeResult<TKey, TValue>
{
    public KafkaConsumeResult(
        ConsumeResult<byte[]?, byte[]?> consumeResult,
        IKafkaDeserializer<TKey> keyDeserializer,
        IKafkaDeserializer<TValue> valueDeserializer)
    {
        Topic = consumeResult.Topic;
        Partition = consumeResult.Partition;
        Offset = consumeResult.Offset;
        Key = keyDeserializer.Deserialize(consumeResult.Message.Key);
        Value = valueDeserializer.Deserialize(consumeResult.Message.Value);
        Headers = consumeResult.Message.Headers.ToDictionary();
    }

    public KafkaConsumeResult(
        InboxMessage message,
        IKafkaDeserializer<TKey> keyDeserializer,
        IKafkaDeserializer<TValue> valueDeserializer)
    {
        Topic = message.Topic;
        Partition = message.Partition;
        Offset = message.Offset;
        Key = keyDeserializer.Deserialize(message.Key);
        Value = valueDeserializer.Deserialize(message.Value);
        Headers = message.Headers;
    }

    public string Topic { get; }

    public int Partition { get; }

    public long Offset { get; }

    public TKey? Key { get; }

    public TValue? Value { get; }

    public IReadOnlyDictionary<string, string?>? Headers { get; }
}
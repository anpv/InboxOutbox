using InboxOutbox.Contracts;

namespace InboxOutbox.Implementations;

internal sealed class KafkaProducer<TKey, TValue>(
    string topic,
    RawKafkaProducer producer,
    IKafkaSerializer serializer)
    : IKafkaProducer<TKey, TValue>
{
    public async Task ProduceAsync(
        TKey key,
        TValue value,
        IReadOnlyDictionary<string, string?>? headers,
        CancellationToken token)
    {
        var rawKey = serializer.SerializeKey(key);
        var rawValue = serializer.SerializeValue(value);

        await producer.ProduceAsync(topic, rawKey, rawValue, headers, token);
    }

    public async Task ProduceAsync(IEnumerable<ProducerRecord<TKey, TValue>> records, CancellationToken token)
    {
        var tasks = records.Select(x => ProduceAsync(x.Key, x.Value, x.Headers, token));

        await Task.WhenAll(tasks);
    }
}
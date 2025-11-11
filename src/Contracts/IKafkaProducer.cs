namespace InboxOutbox.Contracts;

public interface IKafkaProducer<TKey, TValue>
{
    Task ProduceAsync(
        TKey key,
        TValue value,
        IReadOnlyDictionary<string, string?>? headers,
        CancellationToken token);

    Task ProduceAsync(IEnumerable<ProducerRecord<TKey, TValue>> records, CancellationToken token);
}
namespace InboxOutbox.Contracts;

public static class ProducerRecord
{
    public static ProducerRecord<TKey, TValue> Create<TKey, TValue>(
        TKey key,
        TValue value,
        IReadOnlyDictionary<string, string?>? headers) => new(key, value, headers);
}

public sealed record ProducerRecord<TKey, TValue>(
    TKey Key,
    TValue Value,
    IReadOnlyDictionary<string, string?>? Headers);
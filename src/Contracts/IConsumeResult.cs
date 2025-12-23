namespace InboxOutbox.Contracts;

public interface IConsumeResult<out TKey, out TValue>
{
    string Topic { get; }

    int Partition { get; }

    long Offset { get; }

    TKey? Key { get; }

    TValue? Value { get; }

    IReadOnlyDictionary<string, string?>? Headers { get; }
}
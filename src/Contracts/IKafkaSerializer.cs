namespace InboxOutbox.Contracts;

public interface IKafkaSerializer
{
    byte[]? SerializeKey<TKey>(TKey key);

    byte[]? SerializeValue<TValue>(TValue value);
}
namespace InboxOutbox.Contracts;

public interface IKafkaDeserializer<out T>
{
    T? Deserialize(byte[]? data);
}
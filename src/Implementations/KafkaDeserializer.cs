using System.Text.Json;
using InboxOutbox.Contracts;

namespace InboxOutbox.Implementations;

public sealed class KafkaDeserializer<T>(Func<byte[], T> func) : IKafkaDeserializer<T>
{
    public static readonly KafkaDeserializer<T> Json = new(
        x => JsonSerializer.Deserialize<T>(x, JsonSerializerOptions.Web)!);

    public T? Deserialize(byte[]? data) => data is null ? default : func(data);
}
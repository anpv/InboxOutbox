using System.Text;
using System.Text.Json;
using InboxOutbox.Contracts;

namespace InboxOutbox.Implementations;

public sealed class KafkaSerializer(JsonSerializerOptions options) : IKafkaSerializer
{
    public static readonly KafkaSerializer Default = new(JsonSerializerOptions.Web);

    public byte[]? SerializeKey<TKey>(TKey key)
    {
        return key switch
        {
            null => null,
            string value => Encoding.UTF8.GetBytes(value),
            _ => throw new NotSupportedException($"Key of type {key.GetType()} is not supported")
        };
    }

    public byte[]? SerializeValue<TValue>(TValue value)
    {
        return value != null
            ? JsonSerializer.SerializeToUtf8Bytes(value, options)
            : null;
    }
}
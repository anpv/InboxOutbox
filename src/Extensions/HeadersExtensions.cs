using System.Text;
using Confluent.Kafka;

namespace InboxOutbox.Extensions;

public static class HeadersExtensions
{
    public static IReadOnlyDictionary<string, string?>? ToDictionary(this Headers? headers)
    {
        return headers?.ToDictionary(x => x.Key, GetValue);

        static string? GetValue(IHeader header) =>
            header.GetValueBytes() is { } valueBytes
                ? Encoding.UTF8.GetString(valueBytes)
                : null;
    }
}
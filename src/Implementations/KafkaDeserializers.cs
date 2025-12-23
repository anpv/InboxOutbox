using System.Text;
using InboxOutbox.Contracts;

namespace InboxOutbox.Implementations;

public static class KafkaDeserializers
{
    public static readonly IKafkaDeserializer<string> Utf8 = new KafkaDeserializer<string>(x => Encoding.UTF8.GetString(x));
}
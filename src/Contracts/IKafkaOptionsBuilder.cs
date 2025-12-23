using InboxOutbox.Implementations;

namespace InboxOutbox.Contracts;

public interface IKafkaOptionsBuilder
{
    IKafkaOptionsBuilder AddProducer<TKey, TValue>(string topic, IKafkaSerializer? serializer = null);

    IKafkaOptionsBuilder AddConsumer<TValue, TConsumer>(string topic)
        where TConsumer : class, IKafkaConsumer<string, TValue> =>
        AddConsumer<string, TValue, TConsumer>(topic, KafkaDeserializers.Utf8, KafkaDeserializer<TValue>.Json);
    
    IKafkaOptionsBuilder AddConsumer<TKey, TValue, TConsumer>(
        string topic,
        IKafkaDeserializer<TKey> keyDeserializer,
        IKafkaDeserializer<TValue> valueDeserializer)
        where TConsumer : class, IKafkaConsumer<TKey, TValue>;
}
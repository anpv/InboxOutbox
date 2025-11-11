namespace InboxOutbox.Contracts;

public interface IKafkaOptionsBuilder
{
    IKafkaOptionsBuilder AddProducer<TKey, TValue>(string topic, IKafkaSerializer? serializer = null);
}
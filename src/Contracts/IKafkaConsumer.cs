namespace InboxOutbox.Contracts;

public interface IKafkaConsumer<in TKey, in TValue>
{
    Task ConsumeAsync(IConsumeResult<TKey, TValue> consumeResult, CancellationToken token);
}
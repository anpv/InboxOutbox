namespace InboxOutbox.Contracts;

public interface IKafkaConsumerWorker
{
    Task RunAsync(CancellationToken token);
}
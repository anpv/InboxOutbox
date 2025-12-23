using InboxOutbox.Contracts;

namespace InboxOutbox.BackgroundServices;

public sealed class KafkaConsumingBackgroundService(IEnumerable<IKafkaConsumerWorker> workers)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var tasks = workers.Select(x => x.RunAsync(stoppingToken));
        await Task.WhenAll(tasks);
    }
}
using InboxOutbox.Contracts;
using InboxOutbox.Events;

namespace InboxOutbox.Implementations;

public sealed class MeasurementAddedConsumer(ILogger<MeasurementAddedConsumer> logger)
    : IKafkaConsumer<string, MeasurementAdded>
{
    public Task ConsumeAsync(IConsumeResult<string, MeasurementAdded> consumeResult, CancellationToken token)
    {
        logger.LogInformation(
            """
            Consumed:
                Topic={Topic}
                Partition={Partition}
                Offset={Offset}
                Key={Key}
                Value={Value}
                Headers={Headers}
            """,
            consumeResult.Topic,
            consumeResult.Partition,
            consumeResult.Offset,
            consumeResult.Key,
            consumeResult.Value,
            string.Join(",", consumeResult.Headers?.Select(h => $"{h.Key}:{h.Value}") ?? []));
        
        return Task.CompletedTask;
    }
}
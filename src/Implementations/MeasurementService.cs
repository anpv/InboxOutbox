using InboxOutbox.Contracts;
using InboxOutbox.Entities;
using InboxOutbox.Events;

namespace InboxOutbox.Implementations;

public sealed class MeasurementService(
    AppDbContext dbContext,
    IKafkaProducer<string, MeasurementAdded> producer,
    TransactionScopeFactory transactionScopeFactory)
{
    public async Task AddRange(IEnumerable<Measurement> measurements, CancellationToken token)
    {
        using var transactionScope = transactionScopeFactory.Create();

        foreach (var chunk in measurements.Chunk(1000))
        {
            await dbContext.AddRangeAsync(chunk, token);
            await dbContext.SaveChangesAsync(token);
            await SendEvents(chunk, token);
        }

        transactionScope.Complete();
    }

    private async Task SendEvents(IEnumerable<Measurement> measurements, CancellationToken token)
    {
        var records = measurements.Select(CreateProducerRecord);

        await producer.ProduceAsync(records, token);
    }

    private static ProducerRecord<string, MeasurementAdded> CreateProducerRecord(Measurement measurement)
    {
        var key = Guid.CreateVersion7().ToString();
        var value = new MeasurementAdded(measurement.Id, measurement.Value);
        var headers = new Dictionary<string, string?>
        {
            ["CreatedAt"] = measurement.CreatedAt.ToString(),
        };

        return ProducerRecord.Create(key, value, headers);
    }
}
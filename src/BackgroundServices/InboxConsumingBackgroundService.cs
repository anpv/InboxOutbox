using InboxOutbox.Entities;
using InboxOutbox.Extensions;
using InboxOutbox.Implementations;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;
using RawConsumeResult = Confluent.Kafka.ConsumeResult<byte[]?, byte[]?>;

namespace InboxOutbox.BackgroundServices;

public sealed class InboxConsumingBackgroundService(
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    IEnumerable<RawKafkaConsumer> consumers,
    TransactionScopeFactory transactionScopeFactory,
    IOptions<InboxOptions> options,
    ILogger<InboxConsumingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var tasks = consumers.Select(x => RunAsync(x, stoppingToken));
        var allTask = Task.WhenAll(tasks);

        try
        {
            await allTask;
        }
        catch
        {
            logger.LogError(allTask.Exception, "Consuming background service failed");
        }
    }

    private async Task RunAsync(RawKafkaConsumer consumer, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (await ConsumeBatchAsync(consumer, token) is not { Count: > 0 } consumeResults)
            {
                continue;
            }

            try
            {
                using var transactionScope = transactionScopeFactory.Create();

                await StoreAsync(consumeResults, token);
                await consumer.CommitAsync(token);

                transactionScope.Complete();
            }
            catch (OperationCanceledException e) when (e.CancellationToken == token)
            {
                // Ignore
            }
            catch (Exception e)
            {
                logger.LogError(e, "Store consume results failed");
                await consumer.ResetAsync(token);
            }
        }
    }

    private async Task<IReadOnlyCollection<RawConsumeResult>> ConsumeBatchAsync(
        RawKafkaConsumer consumer,
        CancellationToken token)
    {
        try
        {
            return await consumer.ConsumeBatchAsync(options.Value.BatchSize, options.Value.BatchTimeout, token);
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                logger.LogError(e, "Consume batch failed");
            }
            
            return [];
        }
    }

    private async Task StoreAsync(IReadOnlyCollection<RawConsumeResult> consumeResults, CancellationToken token)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<InboxStorage>();
        var messages = consumeResults.Select(CreateMessage);
        await storage.AddRangeAsync(messages, token);
    }

    private InboxMessage CreateMessage(RawConsumeResult consumeResult) =>
        new()
        {
            Topic = consumeResult.Topic,
            Partition = consumeResult.Partition,
            Offset = consumeResult.Offset,
            Key = consumeResult.Message.Key,
            Value = consumeResult.Message.Value,
            Headers = consumeResult.Message.Headers.ToDictionary(),
            CreatedAt = timeProvider.GetUtcNow()
        };
}
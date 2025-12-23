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
    RawKafkaConsumer consumer,
    IOptions<InboxOptions> options,
    ILogger<InboxConsumingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await ConsumeBatchAsync(stoppingToken) is not { Count: > 0 } consumeResults)
            {
                continue;
            }

            var isStored = await StoreAsync(consumeResults, stoppingToken);
            await ConfirmAsync(isStored, stoppingToken);
        }
    }

    private async Task<IReadOnlyCollection<RawConsumeResult>> ConsumeBatchAsync(CancellationToken token)
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

    private async Task<bool> StoreAsync(IReadOnlyCollection<RawConsumeResult> consumeResults, CancellationToken token)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var storage = scope.ServiceProvider.GetRequiredService<InboxStorage>();

            var messages = consumeResults.Select(CreateMessage);
            await storage.AddRangeAsync(messages, token);
            
            return true;
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                logger.LogError(e, "Store failed");
            }

            return false;
        }
    }

    private async Task ConfirmAsync(bool isStored, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await (isStored ? consumer.CommitAsync(token) : consumer.ResetAsync(token));
                
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Confirm failed. Is stored={IsStored}", isStored);
            }
        }
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
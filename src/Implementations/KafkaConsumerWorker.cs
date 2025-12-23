using InboxOutbox.Contracts;
using RawConsumeResult = Confluent.Kafka.ConsumeResult<byte[]?, byte[]?>;

namespace InboxOutbox.Implementations;

public sealed class KafkaConsumerWorker<TKey, TValue>(
    IServiceScopeFactory serviceScopeFactory,
    RawKafkaConsumer consumer,
    ILogger<KafkaConsumerWorker<TKey, TValue>> logger,
    IKafkaDeserializer<TKey> keyDeserializer,
    IKafkaDeserializer<TValue> valueDeserializer)
    : IKafkaConsumerWorker
{
    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (await ConsumeAsync(token) is not { } consumeResult)
            {
                continue;
            }

            await HandleAsync(consumeResult, token);
            await CommitAsync(token);
        }
    }

    private async Task<RawConsumeResult?> ConsumeAsync(CancellationToken token)
    {
        try
        {
            return await consumer.ConsumeAsync(token);
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                logger.LogError(e, "Consume failed");
            }

            return null;
        }
    }

    private async Task HandleAsync(RawConsumeResult result, CancellationToken token)
    {
        try
        {
            var consumeResult = new KafkaConsumeResult<TKey, TValue>(result, keyDeserializer, valueDeserializer);
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IKafkaConsumer<TKey, TValue>>();
            await handler.ConsumeAsync(consumeResult, token);
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                logger.LogError(e, "Handle failed");
            }
        }
    }

    private async Task CommitAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await consumer.CommitAsync(token);
                
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Commit failed");
            }
        }
    }
}
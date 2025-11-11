using InboxOutbox.Options;
using Microsoft.Extensions.Options;

namespace InboxOutbox.Implementations;

public sealed class OutboxBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasMore = await ProcessAsync(stoppingToken);

                if (!hasMore)
                {
                    await Task.Delay(options.Value.EmptyDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
            {
                // Ignore, graceful shutdown
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed publish messages to Kafka");
                await Task.Delay(options.Value.ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessAsync(CancellationToken token)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

        return await processor.ProcessAsync(token);
    }
}
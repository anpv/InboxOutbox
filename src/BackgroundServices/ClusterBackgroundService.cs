using InboxOutbox.Implementations;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;

namespace InboxOutbox.BackgroundServices;

public sealed class ClusterBackgroundService(
    ClusterService clusterService,
    IOptions<ClusterOptions> options,
    ILogger<ClusterBackgroundService> logger)
    : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsKeepAliveEnabled)
        {
            return;
        }

        await clusterService.InitializeAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsKeepAliveEnabled)
        {
            return;
        }

        await clusterService.FinalizeAsync(CancellationToken.None);
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var timer = new PeriodicTimer(options.Value.KeepAliveInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await clusterService.RefreshAliveAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Refresh alive failed");
            }
        }
    }
}
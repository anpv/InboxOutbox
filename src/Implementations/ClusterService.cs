using InboxOutbox.Contracts;
using InboxOutbox.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace InboxOutbox.Implementations;

public sealed class ClusterService : IClusterService
{
    private readonly IDistributedCache _cache;
    private readonly ClusterOptions _options;
    private readonly string _serviceName;
    private readonly string _aliveCacheKey;

    public ClusterService(
        IDistributedCache cache,
        IHostEnvironment hostEnvironment,
        IOptions<ClusterOptions> options)
    {
        _cache = cache;
        _options = options.Value;
        _serviceName = hostEnvironment.ApplicationName;
        CurrentInstanceId = Guid.NewGuid();
        _aliveCacheKey = CreateAliveCacheKey(CurrentInstanceId);
    }

    public Guid CurrentInstanceId { get; }

    public async Task<bool> IsAliveAsync(Guid instanceId, CancellationToken token)
    {
        if (instanceId == CurrentInstanceId)
        {
            return true;
        }

        var key = CreateAliveCacheKey(instanceId);

        return await _cache.GetStringAsync(key, token) != null;
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        var entryOptions = new DistributedCacheEntryOptions()
            .SetSlidingExpiration(_options.KeepAliveTimeout);

        await _cache.SetStringAsync(_aliveCacheKey, string.Empty, entryOptions, token);
    }

    public async Task FinalizeAsync(CancellationToken token) => await _cache.RemoveAsync(_aliveCacheKey, token);

    public async Task RefreshAliveAsync(CancellationToken token) => await _cache.RefreshAsync(_aliveCacheKey, token);

    private string CreateAliveCacheKey(Guid id) => $"Svc:{_serviceName}:Id:{id}:Alive";
}
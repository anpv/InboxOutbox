namespace InboxOutbox.Contracts;

public interface IClusterService
{
    Guid CurrentInstanceId { get; }

    Task<bool> IsAliveAsync(Guid instanceId, CancellationToken token);
}
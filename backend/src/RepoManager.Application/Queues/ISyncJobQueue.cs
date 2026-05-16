namespace RepoManager.Application.Queues;

public sealed record SyncJob(Guid RepositorySyncId, Guid RepositoryId);

public interface ISyncJobQueue
{
    ValueTask EnqueueAsync(SyncJob job, CancellationToken ct = default);
    ValueTask<SyncJob> DequeueAsync(CancellationToken ct = default);
}

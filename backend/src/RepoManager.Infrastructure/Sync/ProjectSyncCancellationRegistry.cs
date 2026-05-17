using System.Collections.Concurrent;

namespace RepoManager.Infrastructure.Sync;

public sealed class ProjectSyncCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationToken Register(Guid projectSyncId)
    {
        var cts = new CancellationTokenSource();
        _sources[projectSyncId] = cts;
        return cts.Token;
    }

    public bool TryCancel(Guid projectSyncId)
    {
        if (_sources.TryGetValue(projectSyncId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    public void Unregister(Guid projectSyncId)
    {
        if (_sources.TryRemove(projectSyncId, out var cts))
            cts.Dispose();
    }
}

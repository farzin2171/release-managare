using System.Threading.Channels;
using RepoManager.Application.Queues;

namespace RepoManager.Infrastructure.Sync;

public class InMemorySyncJobQueue : ISyncJobQueue
{
    private readonly Channel<SyncJob> _channel = Channel.CreateBounded<SyncJob>(
        new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ValueTask EnqueueAsync(SyncJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    public ValueTask<SyncJob> DequeueAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAsync(ct);
}

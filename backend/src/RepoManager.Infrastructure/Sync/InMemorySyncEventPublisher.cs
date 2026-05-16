using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RepoManager.Application.Events;

namespace RepoManager.Infrastructure.Sync;

public class InMemorySyncEventPublisher : ISyncEventPublisher
{
    private readonly ConcurrentDictionary<Guid, Channel<SyncEvent>> _channels = new();

    public void CreateChannel(Guid syncId)
    {
        var channel = Channel.CreateBounded<SyncEvent>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });
        _channels[syncId] = channel;
    }

    public async ValueTask PublishAsync(Guid syncId, SyncEvent evt, CancellationToken ct = default)
    {
        if (_channels.TryGetValue(syncId, out var channel))
            await channel.Writer.WriteAsync(evt, ct);
    }

    public async IAsyncEnumerable<SyncEvent> SubscribeAsync(
        Guid syncId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_channels.TryGetValue(syncId, out var channel))
            yield break;

        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            yield return evt;
    }

    public void CloseChannel(Guid syncId)
    {
        if (!_channels.TryGetValue(syncId, out var channel))
            return;

        channel.Writer.TryComplete();

        // Remove channel from dict after 30 minutes to allow late reconnects to drain
        _ = Task.Delay(TimeSpan.FromMinutes(30)).ContinueWith(
            t => _channels.TryRemove(syncId, out Channel<SyncEvent>? _),
            TaskScheduler.Default);
    }
}

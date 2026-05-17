using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RepoManager.Application.Events;

namespace RepoManager.Infrastructure.Sync;

public sealed class InMemoryProjectSyncEventPublisher : IProjectSyncEventPublisher
{
    private sealed class StreamState
    {
        public Channel<ProjectSseMessage> Channel { get; } =
            System.Threading.Channels.Channel.CreateBounded<ProjectSseMessage>(
                new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        public List<ProjectSseMessage> Buffer { get; } = new(50);
        public long NextId;
        public ProjectSseMessage? CompletionEvent;
    }

    private readonly ConcurrentDictionary<Guid, StreamState> _streams = new();

    public void OpenStream(Guid projectSyncId) =>
        _streams[projectSyncId] = new StreamState();

    public async ValueTask PublishAsync(Guid projectSyncId, string @event, string data, CancellationToken ct = default)
    {
        if (!_streams.TryGetValue(projectSyncId, out var state)) return;

        var id = Interlocked.Increment(ref state.NextId);
        var msg = new ProjectSseMessage(id, @event, data);

        lock (state.Buffer)
        {
            if (state.Buffer.Count >= 50) state.Buffer.RemoveAt(0);
            state.Buffer.Add(msg);
        }

        if (@event == "project_complete") state.CompletionEvent = msg;

        await state.Channel.Writer.WriteAsync(msg, ct);
    }

    public async IAsyncEnumerable<ProjectSseMessage> SubscribeAsync(
        Guid projectSyncId,
        long? lastEventId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_streams.TryGetValue(projectSyncId, out var state)) yield break;

        if (lastEventId.HasValue)
        {
            List<ProjectSseMessage> replay;
            lock (state.Buffer)
                replay = state.Buffer.Where(m => m.Id > lastEventId.Value).ToList();
            foreach (var msg in replay)
                yield return msg;
        }

        await foreach (var msg in state.Channel.Reader.ReadAllAsync(ct))
            yield return msg;
    }

    public void CloseStream(Guid projectSyncId)
    {
        if (_streams.TryGetValue(projectSyncId, out var state))
            state.Channel.Writer.TryComplete();

        _ = Task.Delay(TimeSpan.FromMinutes(30))
            .ContinueWith(t => _streams.TryRemove(projectSyncId, out _), TaskScheduler.Default);
    }

    public ProjectSseMessage? GetCompletionEvent(Guid projectSyncId) =>
        _streams.TryGetValue(projectSyncId, out var state) ? state.CompletionEvent : null;
}

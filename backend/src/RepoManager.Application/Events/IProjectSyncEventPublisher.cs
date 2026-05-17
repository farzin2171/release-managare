namespace RepoManager.Application.Events;

public sealed record ProjectSseMessage(long Id, string Event, string Data);

public interface IProjectSyncEventPublisher
{
    void OpenStream(Guid projectSyncId);
    ValueTask PublishAsync(Guid projectSyncId, string @event, string data, CancellationToken ct = default);
    IAsyncEnumerable<ProjectSseMessage> SubscribeAsync(Guid projectSyncId, long? lastEventId, CancellationToken ct = default);
    void CloseStream(Guid projectSyncId);
    ProjectSseMessage? GetCompletionEvent(Guid projectSyncId);
}

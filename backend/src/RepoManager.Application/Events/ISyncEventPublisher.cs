using RepoManager.Domain.Enums;

namespace RepoManager.Application.Events;

public sealed record SyncCounts(int CommitCount, int TicketCount, int ContributorCount, int BreakingChangeCount);

public sealed record SyncEvent(
    string Type,
    Guid RepoId,
    string RepoName,
    SyncStatus Status,
    string? CurrentStep,
    long ElapsedMs,
    SyncCounts Counts
);

public interface ISyncEventPublisher
{
    void CreateChannel(Guid syncId);
    ValueTask PublishAsync(Guid syncId, SyncEvent evt, CancellationToken ct = default);
    IAsyncEnumerable<SyncEvent> SubscribeAsync(Guid syncId, CancellationToken ct = default);
    void CloseChannel(Guid syncId);
}

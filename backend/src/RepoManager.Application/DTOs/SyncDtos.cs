using RepoManager.Domain.Enums;

namespace RepoManager.Application.DTOs;

public sealed record ContributorSnapshotDto(string Name, string Email, int Commits);

public sealed record RepositorySyncDto(
    Guid Id,
    Guid RepositoryId,
    Guid? ProjectSyncId,
    string FromTag,
    string? ToCommitSha,
    SyncStatus Status,
    string? SkipReason,
    string? CurrentStep,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int CommitCount,
    int TicketCount,
    int ContributorCount,
    int BreakingChangeCount,
    List<ContributorSnapshotDto> Contributors,
    string? ErrorMessage
);

public sealed record ProjectSyncDto(
    Guid Id,
    Guid ProjectId,
    ProjectSyncStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int TotalRepos,
    int SucceededCount,
    int FailedCount,
    int SkippedCount,
    Guid TriggeredByUserId,
    List<RepositorySyncDto>? ChildSyncs
);

public sealed record RepoSyncSnapshotItemDto(
    Guid RepositoryId,
    string RepositoryName,
    string? LatestTag,
    RepositorySyncDto? LatestSync,
    string? CurrentStep
);

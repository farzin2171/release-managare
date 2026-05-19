using RepoManager.Domain.Enums;

namespace RepoManager.Application.Jira.Dtos;

public record RepoJiraComparisonDto(
    Guid RepositoryId,
    string RepositoryName,
    string? CurrentTag,
    string? NextVersion,
    string? JiraFixVersionName,
    bool JiraFixVersionExists,
    bool Supported,
    string? UnsupportedReason,
    ComparisonCounts Counts,
    decimal MatchRate,
    HealthBand Health,
    IReadOnlyList<TicketSummaryDto> InBoth,
    IReadOnlyList<TicketSummaryDto> JiraOnly,
    IReadOnlyList<TicketSummaryDto> GitOnly,
    IReadOnlyList<CommitSummaryDto> UnmatchedCommits,
    DateTime LastSyncedAt
);

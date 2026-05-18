namespace RepoManager.Application.Jira.Dtos;

public record ComparisonCounts(
    int CommitCount,
    int GitTicketCount,
    int JiraTicketCount,
    int InBothCount,
    int JiraOnlyCount,
    int GitOnlyCount
);

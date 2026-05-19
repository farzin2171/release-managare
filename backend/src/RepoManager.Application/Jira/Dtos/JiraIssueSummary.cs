namespace RepoManager.Application.Jira.Dtos;

public record JiraIssueSummary(
    string Key,
    string Summary,
    string Status,
    string StatusCategory,
    string? AssigneeAvatarUrl
);

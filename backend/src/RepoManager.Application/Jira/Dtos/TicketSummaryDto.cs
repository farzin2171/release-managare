namespace RepoManager.Application.Jira.Dtos;

public record TicketSummaryDto(
    string Key,
    string? Summary,
    string? Status,
    string? StatusCategory,
    string? AssigneeAvatarUrl,
    int CommitCount
);

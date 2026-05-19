namespace RepoManager.Application.Jira.Dtos;

public record CommitSummaryDto(
    string Sha,
    string AuthorName,
    string Message
);

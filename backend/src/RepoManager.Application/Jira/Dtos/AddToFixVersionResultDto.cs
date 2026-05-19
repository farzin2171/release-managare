namespace RepoManager.Application.Jira.Dtos;

public record AddToFixVersionResultDto(
    bool Success,
    string JiraFixVersionName,
    bool FixVersionCreated
);

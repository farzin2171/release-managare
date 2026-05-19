namespace RepoManager.Application.Jira.Dtos;

public record ProjectJiraCoverageDto(
    Guid ProjectId,
    string ProjectName,
    int TotalRepoCount,
    int GreenRepoCount,
    int AttentionRepoCount,
    decimal ProjectMatchRate,
    IReadOnlyList<RepoJiraComparisonDto> Repos
);

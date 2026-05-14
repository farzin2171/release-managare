namespace RepoManager.Application.Commits;

public interface IConventionalCommitParser
{
    ParsedCommit Parse(string commitMessage);
}

public record ParsedCommit(
    string? Type,
    string? Scope,
    string? Description,
    bool IsBreaking,
    bool IsConventional,
    string? JiraTicketId
);

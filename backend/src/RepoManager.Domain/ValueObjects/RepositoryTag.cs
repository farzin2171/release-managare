namespace RepoManager.Domain.ValueObjects;

public sealed record RepositoryTag(
    string Name,
    string CommitSha,
    DateTimeOffset? CommitDate,
    string? AuthorName);

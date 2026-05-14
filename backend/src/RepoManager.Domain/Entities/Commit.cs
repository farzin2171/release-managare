namespace RepoManager.Domain.Entities;

public class Commit
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string Sha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTimeOffset CommittedAt { get; set; }
    public string? Type { get; set; }
    public string? Scope { get; set; }
    public string? Description { get; set; }
    public bool IsBreaking { get; set; } = false;
    public bool IsConventional { get; set; } = false;
    public string? JiraTicketId { get; set; }

    public Repository Repository { get; set; } = null!;
}

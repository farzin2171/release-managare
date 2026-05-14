namespace RepoManager.Domain.Entities;

public class JiraConnection
{
    public Guid Id { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedApiToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastTestedAt { get; set; }
    public string? TestStatus { get; set; }

    public ICollection<JiraRelease> JiraReleases { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
}

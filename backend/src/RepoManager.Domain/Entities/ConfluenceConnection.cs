namespace RepoManager.Domain.Entities;

public class ConfluenceConnection
{
    public Guid Id { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedApiToken { get; set; } = string.Empty;
    public string? ChecklistTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastTestedAt { get; set; }
    public string? LastTestStatus { get; set; }
}

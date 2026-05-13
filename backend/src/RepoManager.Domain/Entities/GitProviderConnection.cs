using RepoManager.Domain.Enums;

namespace RepoManager.Domain.Entities;

public class GitProviderConnection
{
    public Guid Id { get; set; }
    public ProviderType ProviderType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OrganizationUrl { get; set; } = string.Empty;
    public string EncryptedPat { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastTestStatus { get; set; }

    public ICollection<Repository> Repositories { get; set; } = [];
}

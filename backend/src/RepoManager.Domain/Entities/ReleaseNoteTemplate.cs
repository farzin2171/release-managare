namespace RepoManager.Domain.Entities;

public class ReleaseNoteTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;

    public ICollection<Project> Projects { get; set; } = [];
}

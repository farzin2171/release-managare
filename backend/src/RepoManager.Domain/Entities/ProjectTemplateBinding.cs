namespace RepoManager.Domain.Entities;

using RepoManager.Domain.Enums;

public class ProjectTemplateBinding
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TemplateId { get; set; }
    public TemplateBindingKind Kind { get; set; }
    public string PageTitleTemplate { get; set; } = string.Empty;
    public string? ParentPageId { get; set; }
    public bool LinkFromReleaseNotes { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public ReleaseNoteTemplate Template { get; set; } = null!;
}

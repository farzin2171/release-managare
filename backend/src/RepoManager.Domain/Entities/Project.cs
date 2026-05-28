namespace RepoManager.Domain.Entities;

using RepoManager.Domain.Enums;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3B82F6";
    public string? ConfluenceSpaceKey { get; set; }
    public string? ConfluenceParentPageId { get; set; }
    public Guid? JiraConnectionId { get; set; }
    public string JiraProjectKeys { get; set; } = "[]";
    public string? FixVersionPattern { get; set; }
    public bool AutoCreateFixVersion { get; set; } = false;
    public bool MatchSubtasksToParents { get; set; } = false;
    public VersionBumpStrategy VersionBumpStrategy { get; set; } = VersionBumpStrategy.Minor;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public JiraConnection? JiraConnection { get; set; }
    public ICollection<ProjectRepository> ProjectRepositories { get; set; } = [];
    public ICollection<Release> Releases { get; set; } = [];
    public ICollection<ProjectTemplateBinding> TemplateBindings { get; set; } = [];
    public ICollection<ProjectCustomVariable> CustomVariables { get; set; } = [];
}

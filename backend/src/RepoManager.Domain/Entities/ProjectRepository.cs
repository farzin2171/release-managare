namespace RepoManager.Domain.Entities;

public class ProjectRepository
{
    public Guid ProjectId { get; set; }
    public Guid RepositoryId { get; set; }
    public bool IsPrimary { get; set; } = false;

    public Project Project { get; set; } = null!;
    public Repository Repository { get; set; } = null!;
}

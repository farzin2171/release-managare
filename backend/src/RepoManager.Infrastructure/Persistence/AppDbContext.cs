using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RepoManager.Domain.Aggregates;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence.Configurations;

namespace RepoManager.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<GitProviderConnection> GitProviderConnections => Set<GitProviderConnection>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectRepository> ProjectRepositories => Set<ProjectRepository>();
    public DbSet<Commit> Commits => Set<Commit>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<ReleaseRepository> ReleaseRepositories => Set<ReleaseRepository>();
    public DbSet<ReleaseRepositoryTag> ReleaseRepositoryTags => Set<ReleaseRepositoryTag>();
    public DbSet<ReleaseNoteTemplate> ReleaseNoteTemplates => Set<ReleaseNoteTemplate>();
    public DbSet<ConfluenceConnection> ConfluenceConnections => Set<ConfluenceConnection>();
    public DbSet<JiraConnection> JiraConnections => Set<JiraConnection>();
    public DbSet<JiraRelease> JiraReleases => Set<JiraRelease>();
    public DbSet<JiraTicket> JiraTickets => Set<JiraTicket>();
    public DbSet<ReleaseReconciliation> ReleaseReconciliations => Set<ReleaseReconciliation>();
    public DbSet<RepositorySync> RepositorySyncs => Set<RepositorySync>();
    public DbSet<ProjectSync> ProjectSyncs => Set<ProjectSync>();
    public DbSet<RepoJiraComparisonSnapshot> RepoJiraComparisonSnapshots => Set<RepoJiraComparisonSnapshot>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot ORDER BY DateTimeOffset columns — store as long (UTC ms since epoch) instead.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToLongConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetToLongConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new RepositorySyncConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectSyncConfiguration());
        modelBuilder.ApplyConfiguration(new RepoJiraComparisonSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new ReleaseRepositoryConfiguration());

        // Users
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).IsRequired();
            e.Property(u => u.IsActive).HasDefaultValue(true).IsRequired();
            e.Property(u => u.CreatedAt).IsRequired();
        });

        // GitProviderConnections
        modelBuilder.Entity<GitProviderConnection>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.Property(g => g.OrganizationUrl).HasMaxLength(500).IsRequired();
            e.Property(g => g.EncryptedPat).IsRequired();
            e.Property(g => g.IsActive).HasDefaultValue(true).IsRequired();
            e.Property(g => g.LastTestStatus).HasMaxLength(50);
            e.HasMany(g => g.Repositories)
             .WithOne(r => r.GitProviderConnection)
             .HasForeignKey(r => r.GitProviderConnectionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Repositories
        modelBuilder.Entity<Repository>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ExternalId).HasMaxLength(200).IsRequired();
            e.Property(r => r.Name).HasMaxLength(300).IsRequired();
            e.Property(r => r.DefaultBranch).HasMaxLength(200).IsRequired();
            e.Property(r => r.WebUrl).HasMaxLength(500).IsRequired();
            e.Property(r => r.AzureProjectName).HasMaxLength(200).IsRequired();
            e.Property(r => r.IsTracked).HasDefaultValue(false).IsRequired();
            e.Property(r => r.LatestTag).HasMaxLength(255);
            e.Property(r => r.LatestTagCommitSha).HasMaxLength(64);
            e.Property(r => r.LastViewedAt);
            e.HasIndex(r => r.IsTracked);
            e.HasIndex(r => new { r.GitProviderConnectionId, r.ExternalId }).IsUnique();
            e.HasOne(r => r.LatestTagSetBy)
             .WithMany()
             .HasForeignKey(r => r.LatestTagSetByUserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasMany(r => r.Commits)
             .WithOne(c => c.Repository)
             .HasForeignKey(c => c.RepositoryId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Tickets)
             .WithOne(t => t.Repository)
             .HasForeignKey(t => t.RepositoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Projects
        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.Color).HasMaxLength(7).IsRequired();
            e.Property(p => p.JiraProjectKeys).IsRequired().HasDefaultValue("[]");
            e.Property(p => p.FixVersionPattern).HasMaxLength(200);
            e.Property(p => p.AutoCreateFixVersion).HasDefaultValue(false).IsRequired();
            e.Property(p => p.MatchSubtasksToParents).HasDefaultValue(false).IsRequired();
            e.Property(p => p.CreatedAt).IsRequired();
            e.Property(p => p.UpdatedAt).IsRequired();
            e.HasOne(p => p.ReleaseNoteTemplate)
             .WithMany(t => t.Projects)
             .HasForeignKey(p => p.ReleaseNoteTemplateId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.JiraConnection)
             .WithMany(j => j.Projects)
             .HasForeignKey(p => p.JiraConnectionId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasMany(p => p.Releases)
             .WithOne(r => r.Project)
             .HasForeignKey(r => r.ProjectId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.ProjectRepositories)
             .WithOne(pr => pr.Project)
             .HasForeignKey(pr => pr.ProjectId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ProjectRepositories (join)
        modelBuilder.Entity<ProjectRepository>(e =>
        {
            e.HasKey(pr => new { pr.ProjectId, pr.RepositoryId });
            e.Property(pr => pr.IsPrimary).HasDefaultValue(false).IsRequired();
            e.HasOne(pr => pr.Repository)
             .WithMany(r => r.ProjectRepositories)
             .HasForeignKey(pr => pr.RepositoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Commits
        modelBuilder.Entity<Commit>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Sha).HasMaxLength(40).IsRequired();
            e.Property(c => c.ShortSha).HasMaxLength(8).IsRequired();
            e.Property(c => c.Message).IsRequired();
            e.Property(c => c.AuthorName).HasMaxLength(200).IsRequired();
            e.Property(c => c.AuthorEmail).HasMaxLength(256).IsRequired();
            e.Property(c => c.CommittedAt).IsRequired();
            e.Property(c => c.Type).HasMaxLength(50);
            e.Property(c => c.Scope).HasMaxLength(200);
            e.Property(c => c.Description).HasMaxLength(500);
            e.Property(c => c.IsBreaking).HasDefaultValue(false).IsRequired();
            e.Property(c => c.IsConventional).HasDefaultValue(false).IsRequired();
            e.Property(c => c.JiraTicketId).HasMaxLength(50);
            e.HasIndex(c => new { c.RepositoryId, c.Sha }).IsUnique();
            e.HasIndex(c => c.JiraTicketId);
            e.HasIndex(c => c.CommittedAt);
        });

        // Tickets
        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TicketId).HasMaxLength(50).IsRequired();
            e.Property(t => t.FromTag).HasMaxLength(200).IsRequired();
            e.Property(t => t.ToTag).HasMaxLength(200).IsRequired();
            e.Property(t => t.Title).HasMaxLength(500);
            e.Property(t => t.PrimaryType).HasMaxLength(50);
            e.Property(t => t.IsBreaking).IsRequired();
            e.Property(t => t.CommitCount).IsRequired();
            e.Property(t => t.ContributorCount).IsRequired();
            e.Property(t => t.FirstCommittedAt).IsRequired();
            e.Property(t => t.LastCommittedAt).IsRequired();
            e.HasIndex(t => new { t.RepositoryId, t.FromTag, t.ToTag, t.TicketId });
        });

        // Releases
        modelBuilder.Entity<Release>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(200).IsRequired().HasDefaultValue(string.Empty);
            e.Property(r => r.Version).HasMaxLength(50).IsRequired();
            e.Property(r => r.Status).IsRequired();
            e.Property(r => r.GeneratedNotesMarkdown).IsRequired();
            e.Property(r => r.ConfluencePageId).HasMaxLength(100);
            e.Property(r => r.ConfluencePageUrl).HasMaxLength(500);
            e.Property(r => r.CreatedAt).IsRequired();
            e.Property(r => r.EditLockedByUserName).HasMaxLength(200);
            e.HasOne(r => r.CreatedBy)
             .WithMany()
             .HasForeignKey(r => r.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.RepositoryTags)
             .WithOne(rt => rt.Release)
             .HasForeignKey(rt => rt.ReleaseId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reconciliation)
             .WithOne(rc => rc.Release)
             .HasForeignKey<ReleaseReconciliation>(rc => rc.ReleaseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ReleaseRepositoryTags
        modelBuilder.Entity<ReleaseRepositoryTag>(e =>
        {
            e.HasKey(rt => new { rt.ReleaseId, rt.RepositoryId });
            e.Property(rt => rt.FromTag).HasMaxLength(200).IsRequired();
            e.Property(rt => rt.ToTag).HasMaxLength(200).IsRequired();
            e.Property(rt => rt.CommitCount).IsRequired();
            e.HasOne(rt => rt.Repository)
             .WithMany(r => r.ReleaseRepositoryTags)
             .HasForeignKey(rt => rt.RepositoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ReleaseNoteTemplates
        modelBuilder.Entity<ReleaseNoteTemplate>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.ContentTemplate).IsRequired();
            e.Property(t => t.IsDefault).HasDefaultValue(false).IsRequired();
        });

        // ConfluenceConnections
        modelBuilder.Entity<ConfluenceConnection>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.BaseUrl).HasMaxLength(500).IsRequired();
            e.Property(c => c.Username).HasMaxLength(256).IsRequired();
            e.Property(c => c.EncryptedApiToken).IsRequired();
            e.Property(c => c.IsActive).HasDefaultValue(true).IsRequired();
            e.Property(c => c.LastTestStatus).HasMaxLength(50);
        });

        // JiraConnections
        modelBuilder.Entity<JiraConnection>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.BaseUrl).HasMaxLength(500).IsRequired();
            e.Property(j => j.Username).HasMaxLength(256).IsRequired();
            e.Property(j => j.EncryptedApiToken).IsRequired();
            e.Property(j => j.IsActive).HasDefaultValue(true).IsRequired();
            e.Property(j => j.TestStatus).HasMaxLength(50);
            e.HasMany(j => j.JiraReleases)
             .WithOne(r => r.JiraConnection)
             .HasForeignKey(r => r.JiraConnectionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // JiraReleases
        modelBuilder.Entity<JiraRelease>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.JiraProjectKey).HasMaxLength(20).IsRequired();
            e.Property(r => r.JiraVersionId).HasMaxLength(100).IsRequired();
            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(1000);
            e.Property(r => r.IsReleased).IsRequired();
            e.Property(r => r.LastSyncedAt).IsRequired();
            e.HasIndex(r => new { r.JiraConnectionId, r.JiraVersionId }).IsUnique();
            e.HasMany(r => r.JiraTickets)
             .WithOne(t => t.JiraRelease)
             .HasForeignKey(t => t.JiraReleaseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // JiraTickets
        modelBuilder.Entity<JiraTicket>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Key).HasMaxLength(50).IsRequired();
            e.Property(t => t.Summary).HasMaxLength(500).IsRequired();
            e.Property(t => t.Status).HasMaxLength(100).IsRequired();
            e.Property(t => t.StatusCategory).IsRequired();
            e.Property(t => t.IssueType).HasMaxLength(100).IsRequired();
            e.Property(t => t.AssigneeName).HasMaxLength(200);
            e.Property(t => t.AssigneeEmail).HasMaxLength(256);
            e.Property(t => t.Priority).HasMaxLength(50);
            e.Property(t => t.ParentKey).HasMaxLength(50);
            e.Property(t => t.LastSyncedAt).IsRequired();
            e.HasIndex(t => new { t.JiraReleaseId, t.Key }).IsUnique();
        });

        // ReleaseReconciliations
        modelBuilder.Entity<ReleaseReconciliation>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.ReleaseId).IsUnique();
            e.Property(r => r.RunAt).IsRequired();
            e.Property(r => r.MatchedCount).IsRequired();
            e.Property(r => r.JiraOnlyCount).IsRequired();
            e.Property(r => r.GitOnlyCount).IsRequired();
            e.Property(r => r.MatchRatePercent).HasPrecision(5, 2).IsRequired();
            e.Property(r => r.Snapshot).IsRequired();
            e.HasOne(r => r.JiraRelease)
             .WithMany()
             .HasForeignKey(r => r.JiraReleaseId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class DateTimeOffsetToLongConverter()
    : ValueConverter<DateTimeOffset, long>(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

internal sealed class NullableDateTimeOffsetToLongConverter()
    : ValueConverter<DateTimeOffset?, long?>(
        v => v.HasValue ? (long?)v.Value.ToUnixTimeMilliseconds() : null,
        v => v.HasValue ? (DateTimeOffset?)DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoManager.Domain.Aggregates;

namespace RepoManager.Infrastructure.Persistence.Configurations;

internal sealed class ProjectSyncConfiguration : IEntityTypeConfiguration<ProjectSync>
{
    public void Configure(EntityTypeBuilder<ProjectSync> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.StartedAt).IsRequired();
        builder.Property(p => p.TotalRepos).IsRequired();
        builder.Property(p => p.SucceededCount).HasDefaultValue(0).IsRequired();
        builder.Property(p => p.FailedCount).HasDefaultValue(0).IsRequired();
        builder.Property(p => p.SkippedCount).HasDefaultValue(0).IsRequired();

        builder.HasOne(p => p.Project)
               .WithMany()
               .HasForeignKey(p => p.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.TriggeredBy)
               .WithMany()
               .HasForeignKey(p => p.TriggeredByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Covers latest-run lookup per project
        builder.HasIndex(p => new { p.ProjectId, p.Status, p.StartedAt });

        // Unique partial index: only one active (Pending=0 or InProgress=1) run per project
        builder.HasIndex(p => p.ProjectId)
               .HasFilter("Status IN (0, 1)")
               .IsUnique();
    }
}

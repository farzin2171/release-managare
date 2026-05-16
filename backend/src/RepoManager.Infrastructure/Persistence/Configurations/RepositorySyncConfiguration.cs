using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoManager.Domain.Aggregates;

namespace RepoManager.Infrastructure.Persistence.Configurations;

internal sealed class RepositorySyncConfiguration : IEntityTypeConfiguration<RepositorySync>
{
    public void Configure(EntityTypeBuilder<RepositorySync> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FromTag).HasMaxLength(100).IsRequired();
        builder.Property(s => s.ToCommitSha).HasMaxLength(64);
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.SkipReason).HasMaxLength(200);
        builder.Property(s => s.CurrentStep).HasMaxLength(50);
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.CommitCount).HasDefaultValue(0).IsRequired();
        builder.Property(s => s.TicketCount).HasDefaultValue(0).IsRequired();
        builder.Property(s => s.ContributorCount).HasDefaultValue(0).IsRequired();
        builder.Property(s => s.BreakingChangeCount).HasDefaultValue(0).IsRequired();
        builder.Property(s => s.ContributorsJson).HasDefaultValue("[]").IsRequired();
        builder.Property(s => s.ErrorMessage).HasMaxLength(1000);

        builder.HasOne(s => s.Repository)
               .WithMany()
               .HasForeignKey(s => s.RepositoryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ProjectSync)
               .WithMany(p => p.RepositorySyncs)
               .HasForeignKey(s => s.ProjectSyncId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.TriggeredBy)
               .WithMany()
               .HasForeignKey(s => s.TriggeredByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Composite index covering active-snapshot lookup
        builder.HasIndex(s => new { s.RepositoryId, s.FromTag, s.Status, s.StartedAt });

        // Index for project-run rollup query
        builder.HasIndex(s => s.ProjectSyncId);
    }
}

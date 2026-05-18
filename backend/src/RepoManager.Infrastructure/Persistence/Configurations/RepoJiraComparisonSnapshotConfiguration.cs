using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoManager.Domain.Entities;

namespace RepoManager.Infrastructure.Persistence.Configurations;

internal sealed class RepoJiraComparisonSnapshotConfiguration : IEntityTypeConfiguration<RepoJiraComparisonSnapshot>
{
    public void Configure(EntityTypeBuilder<RepoJiraComparisonSnapshot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.RepositoryId, s.NextVersion }).IsUnique();

        builder.HasOne(s => s.Repository)
               .WithMany(r => r.JiraComparisonSnapshots)
               .HasForeignKey(s => s.RepositoryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.CurrentTag).HasMaxLength(64).IsRequired();
        builder.Property(s => s.NextVersion).HasMaxLength(32).IsRequired();
        builder.Property(s => s.JiraFixVersionName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.MatchRate).HasPrecision(5, 4);
        builder.Property(s => s.UnsupportedReason).HasMaxLength(256);
        builder.Property(s => s.LastSyncError).HasMaxLength(1024);
        builder.Property(s => s.InBothJson).HasDefaultValue("[]");
        builder.Property(s => s.JiraOnlyJson).HasDefaultValue("[]");
        builder.Property(s => s.GitOnlyJson).HasDefaultValue("[]");
        builder.Property(s => s.UnmatchedCommitsJson).HasDefaultValue("[]");
    }
}

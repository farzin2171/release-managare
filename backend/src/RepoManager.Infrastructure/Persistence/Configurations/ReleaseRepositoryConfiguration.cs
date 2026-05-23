using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoManager.Domain.Entities;

namespace RepoManager.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseRepositoryConfiguration : IEntityTypeConfiguration<ReleaseRepository>
{
    public void Configure(EntityTypeBuilder<ReleaseRepository> builder)
    {
        builder.HasKey(rr => rr.Id);

        builder.Property(rr => rr.PreviousVersion).HasMaxLength(50).IsRequired();
        builder.Property(rr => rr.NextVersion).HasMaxLength(50).IsRequired();
        builder.Property(rr => rr.BumpType).HasMaxLength(20).IsRequired();
        builder.Property(rr => rr.FromCommitSha).HasMaxLength(64).IsRequired();
        builder.Property(rr => rr.ToCommitSha).HasMaxLength(64).IsRequired();
        builder.Property(rr => rr.CommitCount).IsRequired();
        builder.Property(rr => rr.TicketCount).IsRequired();

        builder.HasOne(rr => rr.Release)
               .WithMany(r => r.ReleaseRepositories)
               .HasForeignKey(rr => rr.ReleaseId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rr => rr.Repository)
               .WithMany()
               .HasForeignKey(rr => rr.RepositoryId)
               .OnDelete(DeleteBehavior.Restrict);

        // A repo appears at most once per release
        builder.HasIndex(rr => new { rr.ReleaseId, rr.RepositoryId }).IsUnique();

        // Enables "all releases for this repo" queries
        builder.HasIndex(rr => rr.RepositoryId);
    }
}

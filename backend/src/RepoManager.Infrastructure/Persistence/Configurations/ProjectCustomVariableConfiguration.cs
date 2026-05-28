using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoManager.Domain.Entities;

namespace RepoManager.Infrastructure.Persistence.Configurations;

public class ProjectCustomVariableConfiguration : IEntityTypeConfiguration<ProjectCustomVariable>
{
    public void Configure(EntityTypeBuilder<ProjectCustomVariable> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Key)
            .IsRequired()
            .HasMaxLength(50);

        builder.ToTable(t => t.HasCheckConstraint("CK_CustomVar_Key", "Key GLOB '[a-zA-Z]*'"));

        builder.Property(v => v.Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt).IsRequired();

        builder.HasIndex(v => new { v.ProjectId, v.Key }).IsUnique();

        builder.HasOne(v => v.Project)
            .WithMany(p => p.CustomVariables)
            .HasForeignKey(v => v.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

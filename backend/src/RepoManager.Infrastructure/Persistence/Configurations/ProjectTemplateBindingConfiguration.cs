using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoManager.Domain.Entities;

namespace RepoManager.Infrastructure.Persistence.Configurations;

public class ProjectTemplateBindingConfiguration : IEntityTypeConfiguration<ProjectTemplateBinding>
{
    public void Configure(EntityTypeBuilder<ProjectTemplateBinding> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Kind)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.ToTable(t => t.HasCheckConstraint("CK_Binding_Kind", "Kind IN ('ReleaseNotes','Checklist','Custom','ReleaseSummary')"));

        builder.Property(b => b.PageTitleTemplate)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.ParentPageId)
            .HasMaxLength(100);

        builder.Property(b => b.LinkFromReleaseNotes)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(b => b.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        // Non-unique index — sort order uniqueness is enforced transactionally in service layer
        builder.HasIndex(b => new { b.ProjectId, b.SortOrder });

        builder.HasOne(b => b.Project)
            .WithMany(p => p.TemplateBindings)
            .HasForeignKey(b => b.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Template)
            .WithMany(t => t.TemplateBindings)
            .HasForeignKey(b => b.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

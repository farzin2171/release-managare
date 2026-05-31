using Microsoft.EntityFrameworkCore.Migrations;
using RepoManager.Infrastructure.Persistence.SeedData;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemReleaseNotesTemplate : Migration
    {
        private const string SystemTemplateId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escape single quotes in the template body for SQLite
            var body = ReleaseNotesTemplateBody.Default.Replace("'", "''");

            // Seed the "Release Notes (Default)" system template
            migrationBuilder.Sql($@"
INSERT INTO ReleaseNoteTemplates (Id, Name, ContentTemplate, IsDefault, IsSystem)
VALUES ('{SystemTemplateId}', 'Release Notes (Default)', '{body}', 0, 1);");

            // Backfill: for every project that has no ReleaseNotes binding, create one using this template
            migrationBuilder.Sql($@"
INSERT INTO TemplateBindings (
    Id, ProjectId, TemplateId, Kind,
    PageTitleTemplate, ParentPageId,
    LinkFromReleaseNotes, SortOrder,
    CreatedAt, UpdatedAt)
SELECT
    lower(hex(randomblob(4))) || '-' ||
    lower(hex(randomblob(2))) || '-4' ||
    lower(substr(hex(randomblob(2)), 2)) || '-9' ||
    lower(substr(hex(randomblob(2)), 2)) || '-' ||
    lower(hex(randomblob(6))),
    p.Id,
    '{SystemTemplateId}',
    'ReleaseNotes',
    '{{{{project.name}}}} {{{{version}}}} — Release Notes',
    NULL,
    0,
    0,
    CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER),
    CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER)
FROM Projects p
WHERE NOT EXISTS (
    SELECT 1 FROM TemplateBindings tb
    WHERE tb.ProjectId = p.Id AND tb.Kind = 'ReleaseNotes'
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM ReleaseNoteTemplates WHERE Id = '{SystemTemplateId}';");
        }
    }
}

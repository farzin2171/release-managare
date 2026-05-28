using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTemplateBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add VersionBumpStrategy to Projects
            migrationBuilder.AddColumn<string>(
                name: "VersionBumpStrategy",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: "Minor");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_VersionBumpStrategy",
                table: "Projects",
                sql: "VersionBumpStrategy IN ('Patch','Minor','Major')");

            // Step 2: Create new tables
            migrationBuilder.CreateTable(
                name: "CustomVariables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomVariables", x => x.Id);
                    table.CheckConstraint("CK_CustomVar_Key", "Key GLOB '[a-zA-Z]*'");
                    table.ForeignKey(
                        name: "FK_CustomVariables_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PageTitleTemplate = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ParentPageId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LinkFromReleaseNotes = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateBindings", x => x.Id);
                    table.CheckConstraint("CK_Binding_Kind", "Kind IN ('ReleaseNotes','Checklist','Custom')");
                    table.ForeignKey(
                        name: "FK_TemplateBindings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateBindings_ReleaseNoteTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ReleaseNoteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomVariables_ProjectId_Key",
                table: "CustomVariables",
                columns: new[] { "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateBindings_ProjectId_SortOrder",
                table: "TemplateBindings",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateBindings_TemplateId",
                table: "TemplateBindings",
                column: "TemplateId");

            // Step 3: Backfill — create one ReleaseNotes binding per project that had a template assigned.
            // Must run BEFORE ReleaseNoteTemplateId column is dropped.
            // Dates stored as Unix milliseconds (DateTimeOffsetToLongConverter convention).
            migrationBuilder.Sql(@"
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
    Id,
    ReleaseNoteTemplateId,
    'ReleaseNotes',
    '{{project.name}} {{version}} — Release Notes',
    NULL,
    0,
    0,
    CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER),
    CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER)
FROM Projects
WHERE ReleaseNoteTemplateId IS NOT NULL;");

            // Step 4: Drop the old FK, index, and column — after backfill
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ReleaseNoteTemplates_ReleaseNoteTemplateId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ReleaseNoteTemplateId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ReleaseNoteTemplateId",
                table: "Projects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore ReleaseNoteTemplateId column
            migrationBuilder.AddColumn<Guid>(
                name: "ReleaseNoteTemplateId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            // Restore values from the first (sort order 0) ReleaseNotes binding per project
            migrationBuilder.Sql(@"
UPDATE Projects
SET ReleaseNoteTemplateId = (
    SELECT TemplateId FROM TemplateBindings
    WHERE TemplateBindings.ProjectId = Projects.Id
      AND TemplateBindings.Kind = 'ReleaseNotes'
      AND TemplateBindings.SortOrder = 0
    LIMIT 1
);");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ReleaseNoteTemplateId",
                table: "Projects",
                column: "ReleaseNoteTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ReleaseNoteTemplates_ReleaseNoteTemplateId",
                table: "Projects",
                column: "ReleaseNoteTemplateId",
                principalTable: "ReleaseNoteTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(name: "CustomVariables");
            migrationBuilder.DropTable(name: "TemplateBindings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_VersionBumpStrategy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VersionBumpStrategy",
                table: "Projects");
        }
    }
}

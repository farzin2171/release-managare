using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKind_ReleaseSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Binding_Kind",
                table: "TemplateBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Binding_Kind",
                table: "TemplateBindings",
                sql: "Kind IN ('ReleaseNotes','Checklist','Custom','ReleaseSummary')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Binding_Kind",
                table: "TemplateBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Binding_Kind",
                table: "TemplateBindings",
                sql: "Kind IN ('ReleaseNotes','Checklist','Custom')");
        }
    }
}

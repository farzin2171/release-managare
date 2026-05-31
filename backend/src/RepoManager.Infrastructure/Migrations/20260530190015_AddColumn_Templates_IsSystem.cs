using Microsoft.EntityFrameworkCore.Migrations;
using RepoManager.Infrastructure.Persistence.SeedData;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddColumn_Templates_IsSystem : Migration
    {
        private static readonly Guid SystemTemplateId = new("c7e3f8a1-2b4d-4e6f-8a9b-0c1d2e3f4a5b");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ReleaseNoteTemplates",
                columns: new[] { "Id", "Name", "ContentTemplate", "IsDefault", "IsSystem" },
                values: new object[] { SystemTemplateId, "Release Summary (Default)", ReleaseSummaryTemplateBody.Default, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ReleaseNoteTemplates",
                keyColumn: "Id",
                keyValue: SystemTemplateId);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRepoJiraComparisonSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastViewedAt",
                table: "Repositories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RepoJiraComparisonSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentTag = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NextVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    JiraFixVersionName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    JiraFixVersionExists = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GitTicketCount = table.Column<int>(type: "INTEGER", nullable: false),
                    JiraTicketCount = table.Column<int>(type: "INTEGER", nullable: false),
                    InBothCount = table.Column<int>(type: "INTEGER", nullable: false),
                    JiraOnlyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GitOnlyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    Supported = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnsupportedReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    InBothJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    JiraOnlyJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    GitOnlyJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    UnmatchedCommitsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncError = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepoJiraComparisonSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepoJiraComparisonSnapshots_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepoJiraComparisonSnapshots_RepositoryId_NextVersion",
                table: "RepoJiraComparisonSnapshots",
                columns: new[] { "RepositoryId", "NextVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepoJiraComparisonSnapshots");

            migrationBuilder.DropColumn(
                name: "LastViewedAt",
                table: "Repositories");
        }
    }
}

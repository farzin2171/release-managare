using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    TotalRepos = table.Column<int>(type: "INTEGER", nullable: false),
                    SucceededCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SkippedCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TriggeredByUserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSyncs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSyncs_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepositorySyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectSyncId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FromTag = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ToCommitSha = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SkipReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CurrentStep = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TicketCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ContributorCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    BreakingChangeCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ContributorsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositorySyncs_ProjectSyncs_ProjectSyncId",
                        column: x => x.ProjectSyncId,
                        principalTable: "ProjectSyncs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RepositorySyncs_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositorySyncs_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSyncs_ProjectId",
                table: "ProjectSyncs",
                column: "ProjectId",
                unique: true,
                filter: "Status IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSyncs_ProjectId_Status_StartedAt",
                table: "ProjectSyncs",
                columns: new[] { "ProjectId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSyncs_TriggeredByUserId",
                table: "ProjectSyncs",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySyncs_ProjectSyncId",
                table: "RepositorySyncs",
                column: "ProjectSyncId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySyncs_RepositoryId_FromTag_Status_StartedAt",
                table: "RepositorySyncs",
                columns: new[] { "RepositoryId", "FromTag", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySyncs_TriggeredByUserId",
                table: "RepositorySyncs",
                column: "TriggeredByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositorySyncs");

            migrationBuilder.DropTable(
                name: "ProjectSyncs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseRepositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NextVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BumpType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FromCommitSha = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ToCommitSha = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TicketCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseRepositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseRepositories_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReleaseRepositories_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseRepositories_ReleaseId_RepositoryId",
                table: "ReleaseRepositories",
                columns: new[] { "ReleaseId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseRepositories_RepositoryId",
                table: "ReleaseRepositories",
                column: "RepositoryId");

            // Backfill: create one legacy row per existing Release using the project's primary repo.
            // INSERT OR IGNORE is idempotent — running Up() twice produces no duplicates.
            migrationBuilder.Sql(@"
INSERT OR IGNORE INTO ReleaseRepositories
    (Id, ReleaseId, RepositoryId, PreviousVersion, NextVersion, BumpType,
     FromCommitSha, ToCommitSha, CommitCount, TicketCount)
SELECT
    lower(hex(randomblob(4))) || '-' ||
    lower(hex(randomblob(2))) || '-4' ||
    substr(lower(hex(randomblob(2))), 2) || '-' ||
    substr('89ab', abs(random()) % 4 + 1, 1) ||
    substr(lower(hex(randomblob(2))), 2) || '-' ||
    lower(hex(randomblob(6))),
    r.Id,
    pr.RepositoryId,
    '',
    r.Version,
    'manual',
    '', '', 0, 0
FROM Releases r
JOIN ProjectRepositories pr ON pr.ProjectId = r.ProjectId AND pr.IsPrimary = 1
WHERE NOT EXISTS (
    SELECT 1 FROM ReleaseRepositories rr
    WHERE rr.ReleaseId = r.Id AND rr.RepositoryId = pr.RepositoryId
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseRepositories");
        }
    }
}

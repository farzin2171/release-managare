using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfluenceConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EncryptedApiToken = table.Column<string>(type: "TEXT", nullable: false),
                    ChecklistTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastTestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastTestStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfluenceConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitProviderConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OrganizationUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EncryptedPat = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastTestStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitProviderConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JiraConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EncryptedApiToken = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastTestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TestStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JiraConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseNoteTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContentTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseNoteTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitProviderConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WebUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AzureProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsTracked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_GitProviderConnections_GitProviderConnectionId",
                        column: x => x.GitProviderConnectionId,
                        principalTable: "GitProviderConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JiraReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JiraConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JiraProjectKey = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    JiraVersionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsReleased = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JiraReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JiraReleases_JiraConnections_JiraConnectionId",
                        column: x => x.JiraConnectionId,
                        principalTable: "JiraConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    ReleaseNoteTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConfluenceSpaceKey = table.Column<string>(type: "TEXT", nullable: true),
                    ConfluenceParentPageId = table.Column<string>(type: "TEXT", nullable: true),
                    JiraConnectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    JiraProjectKeys = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    FixVersionPattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AutoCreateFixVersion = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MatchSubtasksToParents = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_JiraConnections_JiraConnectionId",
                        column: x => x.JiraConnectionId,
                        principalTable: "JiraConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Projects_ReleaseNoteTemplates_ReleaseNoteTemplateId",
                        column: x => x.ReleaseNoteTemplateId,
                        principalTable: "ReleaseNoteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Commits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ShortSha = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthorEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsBreaking = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsConventional = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    JiraTicketId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Commits_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromTag = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ToTag = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrimaryType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsBreaking = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ContributorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstCommittedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastCommittedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JiraTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JiraReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StatusCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AssigneeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AssigneeEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ParentKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JiraTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JiraTickets_JiraReleases_JiraReleaseId",
                        column: x => x.JiraReleaseId,
                        principalTable: "JiraReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRepositories",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRepositories", x => new { x.ProjectId, x.RepositoryId });
                    table.ForeignKey(
                        name: "FK_ProjectRepositories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRepositories_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Releases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedNotesMarkdown = table.Column<string>(type: "TEXT", nullable: false),
                    EditedNotesMarkdown = table.Column<string>(type: "TEXT", nullable: true),
                    ConfluencePageId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ConfluencePageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Releases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Releases_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JiraReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    JiraOnlyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GitOnlyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Snapshot = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseReconciliations_JiraReleases_JiraReleaseId",
                        column: x => x.JiraReleaseId,
                        principalTable: "JiraReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReleaseReconciliations_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseRepositoryTags",
                columns: table => new
                {
                    ReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromTag = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ToTag = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseRepositoryTags", x => new { x.ReleaseId, x.RepositoryId });
                    table.ForeignKey(
                        name: "FK_ReleaseRepositoryTags_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReleaseRepositoryTags_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Commits_CommittedAt",
                table: "Commits",
                column: "CommittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Commits_JiraTicketId",
                table: "Commits",
                column: "JiraTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Commits_RepositoryId_Sha",
                table: "Commits",
                columns: new[] { "RepositoryId", "Sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JiraReleases_JiraConnectionId_JiraVersionId",
                table: "JiraReleases",
                columns: new[] { "JiraConnectionId", "JiraVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JiraTickets_JiraReleaseId_Key",
                table: "JiraTickets",
                columns: new[] { "JiraReleaseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRepositories_RepositoryId",
                table: "ProjectRepositories",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_JiraConnectionId",
                table: "Projects",
                column: "JiraConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ReleaseNoteTemplateId",
                table: "Projects",
                column: "ReleaseNoteTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNoteTemplates_Name",
                table: "ReleaseNoteTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseReconciliations_JiraReleaseId",
                table: "ReleaseReconciliations",
                column: "JiraReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseReconciliations_ReleaseId",
                table: "ReleaseReconciliations",
                column: "ReleaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseRepositoryTags_RepositoryId",
                table: "ReleaseRepositoryTags",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_CreatedByUserId",
                table: "Releases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ProjectId",
                table: "Releases",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_GitProviderConnectionId_ExternalId",
                table: "Repositories",
                columns: new[] { "GitProviderConnectionId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_IsTracked",
                table: "Repositories",
                column: "IsTracked");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RepositoryId_FromTag_ToTag_TicketId",
                table: "Tickets",
                columns: new[] { "RepositoryId", "FromTag", "ToTag", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Commits");

            migrationBuilder.DropTable(
                name: "ConfluenceConnections");

            migrationBuilder.DropTable(
                name: "JiraTickets");

            migrationBuilder.DropTable(
                name: "ProjectRepositories");

            migrationBuilder.DropTable(
                name: "ReleaseReconciliations");

            migrationBuilder.DropTable(
                name: "ReleaseRepositoryTags");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "JiraReleases");

            migrationBuilder.DropTable(
                name: "Releases");

            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "GitProviderConnections");

            migrationBuilder.DropTable(
                name: "JiraConnections");

            migrationBuilder.DropTable(
                name: "ReleaseNoteTemplates");
        }
    }
}

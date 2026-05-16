using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLatestTagToRepositories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LatestTag",
                table: "Repositories",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestTagCommitSha",
                table: "Repositories",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestTagSetAt",
                table: "Repositories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LatestTagSetByUserId",
                table: "Repositories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_LatestTagSetByUserId",
                table: "Repositories",
                column: "LatestTagSetByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Users_LatestTagSetByUserId",
                table: "Repositories",
                column: "LatestTagSetByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Users_LatestTagSetByUserId",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_LatestTagSetByUserId",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LatestTag",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LatestTagCommitSha",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LatestTagSetAt",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LatestTagSetByUserId",
                table: "Repositories");
        }
    }
}

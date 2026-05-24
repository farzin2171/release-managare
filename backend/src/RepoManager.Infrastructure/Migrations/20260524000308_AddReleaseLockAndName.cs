using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseLockAndName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EditLockExpiresAt",
                table: "Releases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EditLockedByUserId",
                table: "Releases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditLockedByUserName",
                table: "Releases",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Releases",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditLockExpiresAt",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "EditLockedByUserId",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "EditLockedByUserName",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Releases");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameByokToOtherChars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "byok_chars_used",
                table: "users",
                newName: "other_chars_used");

            migrationBuilder.AddColumn<long>(
                name: "other_chars_limit",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 50000L);

            migrationBuilder.AddColumn<DateTime>(
                name: "other_chars_reset_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "other_chars_limit",
                table: "users");

            migrationBuilder.DropColumn(
                name: "other_chars_reset_at",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "other_chars_used",
                table: "users",
                newName: "byok_chars_used");
        }
    }
}

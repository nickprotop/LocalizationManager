using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "validation_cache_json",
                table: "projects",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validation_cached_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "validation_cache_json",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "validation_cached_at",
                table: "projects");
        }
    }
}

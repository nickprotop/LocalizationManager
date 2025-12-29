using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFormatAndConfigFromProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_users_config_updated_by",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_config_updated_by",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "config_json",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "config_updated_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "config_updated_by",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "config_version",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "format",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "localization_path",
                table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "github_format",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "github_format",
                table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "config_json",
                table: "projects",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "config_updated_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "config_updated_by",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "config_version",
                table: "projects",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "format",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "localization_path",
                table: "projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_projects_config_updated_by",
                table: "projects",
                column: "config_updated_by");

            migrationBuilder.AddForeignKey(
                name: "FK_projects_users_config_updated_by",
                table: "projects",
                column: "config_updated_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

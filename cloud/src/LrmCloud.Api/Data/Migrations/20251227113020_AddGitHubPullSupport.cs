using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubPullSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_github_pull_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_github_pull_commit",
                table: "projects",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "github_sync_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    key_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    plural_form = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    github_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    github_value = table.Column<string>(type: "text", nullable: true),
                    github_comment = table.Column<string>(type: "text", nullable: true),
                    github_commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_sync_state", x => x.id);
                    table.ForeignKey(
                        name: "FK_github_sync_state_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_github_sync_state_project_id",
                table: "github_sync_state",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_sync_state_project_id_key_name_language_code_plural_~",
                table: "github_sync_state",
                columns: new[] { "project_id", "key_name", "language_code", "plural_form" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_sync_state_synced_at",
                table: "github_sync_state",
                column: "synced_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "github_sync_state");

            migrationBuilder.DropColumn(
                name: "last_github_pull_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "last_github_pull_commit",
                table: "projects");
        }
    }
}

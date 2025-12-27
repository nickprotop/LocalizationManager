using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "github_sync_state",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "pending_conflicts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    key_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    plural_form = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    conflict_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    github_value = table.Column<string>(type: "text", nullable: true),
                    cloud_value = table.Column<string>(type: "text", nullable: true),
                    base_value = table.Column<string>(type: "text", nullable: true),
                    cloud_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cloud_modified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_conflicts", x => x.id);
                    table.ForeignKey(
                        name: "FK_pending_conflicts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pending_conflicts_created_at",
                table: "pending_conflicts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_pending_conflicts_project_id",
                table: "pending_conflicts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_pending_conflicts_project_id_key_name_language_code_plural_~",
                table: "pending_conflicts",
                columns: new[] { "project_id", "key_name", "language_code", "plural_form" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_conflicts");

            migrationBuilder.DropColumn(
                name: "version",
                table: "github_sync_state");
        }
    }
}

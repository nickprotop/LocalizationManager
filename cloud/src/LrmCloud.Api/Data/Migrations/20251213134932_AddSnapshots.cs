using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "message",
                table: "sync_history",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_id",
                table: "sync_history",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_snapshots",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "snapshot_retention_days",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "snapshots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    snapshot_id = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    storage_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_count = table.Column<int>(type: "integer", nullable: false),
                    key_count = table.Column<int>(type: "integer", nullable: false),
                    translation_count = table.Column<int>(type: "integer", nullable: false),
                    snapshot_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_snapshots_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_snapshots_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_created_at",
                table: "snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_created_by_user_id",
                table: "snapshots",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_project_id",
                table: "snapshots",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_project_id_snapshot_id",
                table: "snapshots",
                columns: new[] { "project_id", "snapshot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_snapshot_type",
                table: "snapshots",
                column: "snapshot_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshots");

            migrationBuilder.DropColumn(
                name: "message",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "snapshot_id",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "max_snapshots",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "snapshot_retention_days",
                table: "projects");
        }
    }
}

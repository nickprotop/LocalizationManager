using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSyncHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_conflicts");

            migrationBuilder.DropColumn(
                name: "commit_sha",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "direction",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "pr_url",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "snapshot_id",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "sync_type",
                table: "sync_history");

            migrationBuilder.RenameColumn(
                name: "pr_number",
                table: "sync_history",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "keys_updated",
                table: "sync_history",
                newName: "entries_modified");

            migrationBuilder.RenameColumn(
                name: "keys_deleted",
                table: "sync_history",
                newName: "entries_deleted");

            migrationBuilder.RenameColumn(
                name: "keys_added",
                table: "sync_history",
                newName: "entries_added");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "sync_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "completed",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            // Update existing null status values to 'completed'
            migrationBuilder.Sql("UPDATE sync_history SET status = 'completed' WHERE status IS NULL");

            migrationBuilder.AddColumn<string>(
                name: "changes_json",
                table: "sync_history",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "history_id",
                table: "sync_history",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValueSql: "substring(gen_random_uuid()::text, 1, 8)");

            migrationBuilder.AddColumn<string>(
                name: "operation_type",
                table: "sync_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "push");

            migrationBuilder.AddColumn<int>(
                name: "reverted_from_id",
                table: "sync_history",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_history_project_id_history_id",
                table: "sync_history",
                columns: new[] { "project_id", "history_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_history_reverted_from_id",
                table: "sync_history",
                column: "reverted_from_id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_history_user_id",
                table: "sync_history",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_sync_history_sync_history_reverted_from_id",
                table: "sync_history",
                column: "reverted_from_id",
                principalTable: "sync_history",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_sync_history_users_user_id",
                table: "sync_history",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sync_history_sync_history_reverted_from_id",
                table: "sync_history");

            migrationBuilder.DropForeignKey(
                name: "FK_sync_history_users_user_id",
                table: "sync_history");

            migrationBuilder.DropIndex(
                name: "IX_sync_history_project_id_history_id",
                table: "sync_history");

            migrationBuilder.DropIndex(
                name: "IX_sync_history_reverted_from_id",
                table: "sync_history");

            migrationBuilder.DropIndex(
                name: "IX_sync_history_user_id",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "changes_json",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "history_id",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "operation_type",
                table: "sync_history");

            migrationBuilder.DropColumn(
                name: "reverted_from_id",
                table: "sync_history");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "sync_history",
                newName: "pr_number");

            migrationBuilder.RenameColumn(
                name: "entries_modified",
                table: "sync_history",
                newName: "keys_updated");

            migrationBuilder.RenameColumn(
                name: "entries_deleted",
                table: "sync_history",
                newName: "keys_deleted");

            migrationBuilder.RenameColumn(
                name: "entries_added",
                table: "sync_history",
                newName: "keys_added");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "sync_history",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "commit_sha",
                table: "sync_history",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "direction",
                table: "sync_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "sync_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pr_url",
                table: "sync_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_id",
                table: "sync_history",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sync_type",
                table: "sync_history",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "sync_conflicts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    resolved_by = table.Column<int>(type: "integer", nullable: true),
                    resource_key_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    local_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    local_value = table.Column<string>(type: "text", nullable: true),
                    remote_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    remote_value = table.Column<string>(type: "text", nullable: true),
                    resolution = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_conflicts", x => x.id);
                    table.ForeignKey(
                        name: "FK_sync_conflicts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sync_conflicts_resource_keys_resource_key_id",
                        column: x => x.resource_key_id,
                        principalTable: "resource_keys",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sync_conflicts_users_resolved_by",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_conflicts_project_id",
                table: "sync_conflicts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_conflicts_resolved_by",
                table: "sync_conflicts",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "IX_sync_conflicts_resource_key_id",
                table: "sync_conflicts",
                column: "resource_key_id");
        }
    }
}

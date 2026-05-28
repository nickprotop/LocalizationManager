using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseNameToGitHubSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_github_sync_state_project_id_key_name_language_code_plural_~",
                table: "github_sync_state");

            migrationBuilder.AddColumn<string>(
                name: "base_name",
                table: "github_sync_state",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_github_sync_state_project_id_base_name_key_name_language_co~",
                table: "github_sync_state",
                columns: new[] { "project_id", "base_name", "key_name", "language_code", "plural_form" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_github_sync_state_project_id_base_name_key_name_language_co~",
                table: "github_sync_state");

            migrationBuilder.DropColumn(
                name: "base_name",
                table: "github_sync_state");

            migrationBuilder.CreateIndex(
                name: "IX_github_sync_state_project_id_key_name_language_code_plural_~",
                table: "github_sync_state",
                columns: new[] { "project_id", "key_name", "language_code", "plural_form" },
                unique: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubTokenSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "github_access_token_encrypted",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "github_base_path",
                table: "projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "github_connected_by_user_id",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "github_access_token_encrypted",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "github_connected_by_user_id",
                table: "organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_github_connected_by_user_id",
                table: "projects",
                column: "github_connected_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_github_connected_by_user_id",
                table: "organizations",
                column: "github_connected_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_organizations_users_github_connected_by_user_id",
                table: "organizations",
                column: "github_connected_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_users_github_connected_by_user_id",
                table: "projects",
                column: "github_connected_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_organizations_users_github_connected_by_user_id",
                table: "organizations");

            migrationBuilder.DropForeignKey(
                name: "FK_projects_users_github_connected_by_user_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_github_connected_by_user_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_organizations_github_connected_by_user_id",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "github_access_token_encrypted",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "github_base_path",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "github_connected_by_user_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "github_access_token_encrypted",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "github_connected_by_user_id",
                table: "organizations");
        }
    }
}

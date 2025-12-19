using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "translations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "approved_by_id",
                table: "translations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_comment",
                table: "translations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "translations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "inherit_organization_reviewers",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_approval_before_export",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_review_before_export",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "review_workflow_enabled",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "organization_reviewers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_by_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_reviewers", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_reviewers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_reviewers_users_added_by_id",
                        column: x => x.added_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_organization_reviewers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_reviewers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    language_codes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    added_by_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_reviewers", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_reviewers_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_reviewers_users_added_by_id",
                        column: x => x.added_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_reviewers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translations_approved_by_id",
                table: "translations",
                column: "approved_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_reviewers_added_by_id",
                table: "organization_reviewers",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_reviewers_organization_id",
                table: "organization_reviewers",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_reviewers_organization_id_user_id",
                table: "organization_reviewers",
                columns: new[] { "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_reviewers_user_id",
                table: "organization_reviewers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_reviewers_added_by_id",
                table: "project_reviewers",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_reviewers_project_id",
                table: "project_reviewers",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_reviewers_project_id_user_id",
                table: "project_reviewers",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_reviewers_user_id",
                table: "project_reviewers",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_translations_users_approved_by_id",
                table: "translations",
                column: "approved_by_id",
                principalTable: "users",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_translations_users_approved_by_id",
                table: "translations");

            migrationBuilder.DropTable(
                name: "organization_reviewers");

            migrationBuilder.DropTable(
                name: "project_reviewers");

            migrationBuilder.DropIndex(
                name: "IX_translations_approved_by_id",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "approved_by_id",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "rejection_comment",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "inherit_organization_reviewers",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "require_approval_before_export",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "require_review_before_export",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "review_workflow_enabled",
                table: "projects");
        }
    }
}

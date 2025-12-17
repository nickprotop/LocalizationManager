using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usage_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    acting_user_id = table.Column<int>(type: "integer", nullable: false),
                    billed_user_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    characters_used = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_lrm_provider = table.Column<bool>(type: "boolean", nullable: false),
                    key_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_usage_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_usage_events_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_usage_events_users_acting_user_id",
                        column: x => x.acting_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usage_events_users_billed_user_id",
                        column: x => x.billed_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_acting_user_id",
                table: "usage_events",
                column: "acting_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_billed_user_id",
                table: "usage_events",
                column: "billed_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_created_at",
                table: "usage_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_organization_id",
                table: "usage_events",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_organization_id_created_at",
                table: "usage_events",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_project_id",
                table: "usage_events",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_events_project_id_created_at",
                table: "usage_events",
                columns: new[] { "project_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usage_events");
        }
    }
}

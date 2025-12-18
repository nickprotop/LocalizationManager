using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGlossary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "glossary_provider_sync",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    external_glossary_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sync_error = table.Column<string>(type: "text", nullable: true),
                    entry_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_glossary_provider_sync", x => x.id);
                    table.CheckConstraint("CK_glossary_provider_sync_owner", "(project_id IS NOT NULL AND organization_id IS NULL) OR (project_id IS NULL AND organization_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_glossary_provider_sync_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_glossary_provider_sync_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "glossary_terms",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    source_term = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    case_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_glossary_terms", x => x.id);
                    table.CheckConstraint("CK_glossary_terms_owner", "(project_id IS NOT NULL AND organization_id IS NULL) OR (project_id IS NULL AND organization_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_glossary_terms_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_glossary_terms_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_glossary_terms_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "glossary_translations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    term_id = table.Column<int>(type: "integer", nullable: false),
                    target_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    translated_term = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_glossary_translations", x => x.id);
                    table.ForeignKey(
                        name: "FK_glossary_translations_glossary_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "glossary_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_glossary_provider_sync_organization_id",
                table: "glossary_provider_sync",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_provider_sync_organization_id_provider_name_source~",
                table: "glossary_provider_sync",
                columns: new[] { "organization_id", "provider_name", "source_language", "target_language" },
                unique: true,
                filter: "organization_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_provider_sync_project_id",
                table: "glossary_provider_sync",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_provider_sync_project_id_provider_name_source_lang~",
                table: "glossary_provider_sync",
                columns: new[] { "project_id", "provider_name", "source_language", "target_language" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_terms_created_by",
                table: "glossary_terms",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_terms_organization_id",
                table: "glossary_terms",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_terms_organization_id_source_term_source_language",
                table: "glossary_terms",
                columns: new[] { "organization_id", "source_term", "source_language" },
                unique: true,
                filter: "organization_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_terms_project_id",
                table: "glossary_terms",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_terms_project_id_source_term_source_language",
                table: "glossary_terms",
                columns: new[] { "project_id", "source_term", "source_language" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_translations_term_id",
                table: "glossary_translations",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "IX_glossary_translations_term_id_target_language",
                table: "glossary_translations",
                columns: new[] { "term_id", "target_language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "glossary_provider_sync");

            migrationBuilder.DropTable(
                name: "glossary_translations");

            migrationBuilder.DropTable(
                name: "glossary_terms");
        }
    }
}

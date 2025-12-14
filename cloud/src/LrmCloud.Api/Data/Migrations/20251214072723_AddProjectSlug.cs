using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add slug column as nullable first
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Populate slug from name (convert to lowercase, replace spaces with hyphens, remove special chars)
            migrationBuilder.Sql(@"
                UPDATE projects
                SET slug = LOWER(
                    REGEXP_REPLACE(
                        REGEXP_REPLACE(name, '[^a-zA-Z0-9\s-]', '', 'g'),
                        '\s+', '-', 'g'
                    )
                )
                WHERE slug IS NULL OR slug = '';
            ");

            // Handle duplicates by appending id
            migrationBuilder.Sql(@"
                WITH duplicates AS (
                    SELECT id, slug, ROW_NUMBER() OVER (PARTITION BY slug, user_id ORDER BY id) as rn
                    FROM projects
                    WHERE user_id IS NOT NULL
                )
                UPDATE projects p
                SET slug = d.slug || '-' || d.id
                FROM duplicates d
                WHERE p.id = d.id AND d.rn > 1;
            ");

            migrationBuilder.Sql(@"
                WITH duplicates AS (
                    SELECT id, slug, ROW_NUMBER() OVER (PARTITION BY slug, organization_id ORDER BY id) as rn
                    FROM projects
                    WHERE organization_id IS NOT NULL
                )
                UPDATE projects p
                SET slug = d.slug || '-' || d.id
                FROM duplicates d
                WHERE p.id = d.id AND d.rn > 1;
            ");

            // Make slug not nullable
            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_projects_organization_id_slug",
                table: "projects",
                columns: new[] { "organization_id", "slug" },
                unique: true,
                filter: "organization_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_projects_user_id_slug",
                table: "projects",
                columns: new[] { "user_id", "slug" },
                unique: true,
                filter: "user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_projects_organization_id_slug",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_user_id_slug",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "projects");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceTextToResourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_text",
                table: "resource_keys",
                type: "text",
                nullable: true);

            // Migrate existing data: copy source translations (LanguageCode = '') to ResourceKey.SourceText
            // This handles all formats - they all store default language as empty LanguageCode
            migrationBuilder.Sql(@"
                UPDATE resource_keys rk
                SET source_text = (
                    SELECT t.value
                    FROM translations t
                    WHERE t.resource_key_id = rk.id
                    AND (t.language_code = '' OR t.language_code IS NULL)
                    AND (t.plural_form = '' OR t.plural_form IS NULL)
                    LIMIT 1
                )
                WHERE rk.source_text IS NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_text",
                table: "resource_keys");
        }
    }
}

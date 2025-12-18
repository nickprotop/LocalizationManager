using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translation_memories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    source_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source_text = table.Column<string>(type: "text", nullable: false),
                    translated_text = table.Column<string>(type: "text", nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    use_count = table.Column<int>(type: "integer", nullable: false),
                    context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_memories", x => x.id);
                    table.ForeignKey(
                        name: "FK_translation_memories_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_translation_memories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translation_memories_organization_id",
                table: "translation_memories",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_translation_memories_source_language",
                table: "translation_memories",
                column: "source_language");

            migrationBuilder.CreateIndex(
                name: "IX_translation_memories_target_language",
                table: "translation_memories",
                column: "target_language");

            migrationBuilder.CreateIndex(
                name: "IX_translation_memories_updated_at",
                table: "translation_memories",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "IX_translation_memories_user_id",
                table: "translation_memories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_translation_memories_user_id_source_language_target_languag~",
                table: "translation_memories",
                columns: new[] { "user_id", "source_language", "target_language", "source_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translation_memories");
        }
    }
}

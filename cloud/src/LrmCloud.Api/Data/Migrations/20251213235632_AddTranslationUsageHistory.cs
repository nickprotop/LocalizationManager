using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationUsageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translation_usage_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chars_used = table.Column<long>(type: "bigint", nullable: false),
                    api_calls = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_usage_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_translation_usage_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translation_usage_history_period_start",
                table: "translation_usage_history",
                column: "period_start");

            migrationBuilder.CreateIndex(
                name: "IX_translation_usage_history_provider_name",
                table: "translation_usage_history",
                column: "provider_name");

            migrationBuilder.CreateIndex(
                name: "IX_translation_usage_history_user_id",
                table: "translation_usage_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_translation_usage_history_user_id_provider_name_period_start",
                table: "translation_usage_history",
                columns: new[] { "user_id", "provider_name", "period_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translation_usage_history");
        }
    }
}

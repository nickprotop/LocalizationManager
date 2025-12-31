using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenSelectorToRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "token_selector",
                table: "refresh_tokens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_selector",
                table: "refresh_tokens",
                column: "token_selector",
                unique: true,
                filter: "token_selector IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_token_selector",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token_selector",
                table: "refresh_tokens");
        }
    }
}

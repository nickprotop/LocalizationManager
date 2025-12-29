using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceToSyncHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "sync_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source",
                table: "sync_history");
        }
    }
}

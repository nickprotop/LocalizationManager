using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseNameToResourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resource_keys_project_id_key_name",
                table: "resource_keys");

            migrationBuilder.AddColumn<string>(
                name: "base_name",
                table: "resource_keys",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_resource_keys_project_id_base_name_key_name",
                table: "resource_keys",
                columns: new[] { "project_id", "base_name", "key_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resource_keys_project_id_base_name_key_name",
                table: "resource_keys");

            migrationBuilder.DropColumn(
                name: "base_name",
                table: "resource_keys");

            migrationBuilder.CreateIndex(
                name: "IX_resource_keys_project_id_key_name",
                table: "resource_keys",
                columns: new[] { "project_id", "key_name" },
                unique: true);
        }
    }
}

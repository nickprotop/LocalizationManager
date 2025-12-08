using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "accepted_at",
                table: "organization_members",
                newName: "joined_at");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "organizations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "organizations");

            migrationBuilder.RenameColumn(
                name: "joined_at",
                table: "organization_members",
                newName: "accepted_at");
        }
    }
}

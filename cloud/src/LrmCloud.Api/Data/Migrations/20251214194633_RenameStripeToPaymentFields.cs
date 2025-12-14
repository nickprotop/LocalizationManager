using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LrmCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameStripeToPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "stripe_subscription_id",
                table: "users",
                newName: "payment_subscription_id");

            migrationBuilder.RenameColumn(
                name: "stripe_customer_id",
                table: "users",
                newName: "payment_customer_id");

            migrationBuilder.RenameColumn(
                name: "stripe_customer_id",
                table: "organizations",
                newName: "payment_customer_id");

            migrationBuilder.AddColumn<string>(
                name: "payment_provider",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_provider",
                table: "organizations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Set existing customers to use 'stripe' provider
            migrationBuilder.Sql("UPDATE users SET payment_provider = 'stripe' WHERE payment_customer_id IS NOT NULL");
            migrationBuilder.Sql("UPDATE organizations SET payment_provider = 'stripe' WHERE payment_customer_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_provider",
                table: "users");

            migrationBuilder.DropColumn(
                name: "payment_provider",
                table: "organizations");

            migrationBuilder.RenameColumn(
                name: "payment_subscription_id",
                table: "users",
                newName: "stripe_subscription_id");

            migrationBuilder.RenameColumn(
                name: "payment_customer_id",
                table: "users",
                newName: "stripe_customer_id");

            migrationBuilder.RenameColumn(
                name: "payment_customer_id",
                table: "organizations",
                newName: "stripe_customer_id");
        }
    }
}

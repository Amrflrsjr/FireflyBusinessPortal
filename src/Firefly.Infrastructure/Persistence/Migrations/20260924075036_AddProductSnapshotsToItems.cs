using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firefly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSnapshotsToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "QuotationItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VariantNameSnapshot",
                table: "QuotationItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "InvoiceItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VariantNameSnapshot",
                table: "InvoiceItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "QuotationItems");

            migrationBuilder.DropColumn(
                name: "VariantNameSnapshot",
                table: "QuotationItems");

            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "VariantNameSnapshot",
                table: "InvoiceItems");
        }
    }
}

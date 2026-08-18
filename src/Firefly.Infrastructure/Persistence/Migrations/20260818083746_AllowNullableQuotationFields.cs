using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firefly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullableQuotationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_ProductVariants_ProductVariantId",
                table: "InvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_QuotationItems_ProductVariants_ProductVariantId",
                table: "QuotationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_CustomerContacts_ContactId",
                table: "Quotations");

            migrationBuilder.AlterColumn<string>(
                name: "NoteToCustomer",
                table: "Quotations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "ContactId",
                table: "Quotations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ProductVariantId",
                table: "QuotationItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ProductVariantId",
                table: "InvoiceItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_ProductVariants_ProductVariantId",
                table: "InvoiceItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_ProductVariants_ProductVariantId",
                table: "QuotationItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_CustomerContacts_ContactId",
                table: "Quotations",
                column: "ContactId",
                principalTable: "CustomerContacts",
                principalColumn: "ContactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_ProductVariants_ProductVariantId",
                table: "InvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_QuotationItems_ProductVariants_ProductVariantId",
                table: "QuotationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_CustomerContacts_ContactId",
                table: "Quotations");

            migrationBuilder.AlterColumn<string>(
                name: "NoteToCustomer",
                table: "Quotations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ContactId",
                table: "Quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductVariantId",
                table: "QuotationItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductVariantId",
                table: "InvoiceItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_ProductVariants_ProductVariantId",
                table: "InvoiceItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "ProductVariantId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_ProductVariants_ProductVariantId",
                table: "QuotationItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "ProductVariantId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_CustomerContacts_ContactId",
                table: "Quotations",
                column: "ContactId",
                principalTable: "CustomerContacts",
                principalColumn: "ContactId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

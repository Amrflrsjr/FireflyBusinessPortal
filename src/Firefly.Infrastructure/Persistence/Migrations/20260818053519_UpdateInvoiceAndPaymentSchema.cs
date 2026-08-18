using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firefly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInvoiceAndPaymentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CustomerContacts_ContactId",
                table: "Invoices");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "Payments",
                newName: "AmountPaid");

            migrationBuilder.RenameColumn(
                name: "PaymentStatus",
                table: "Invoices",
                newName: "VATType");

            migrationBuilder.RenameColumn(
                name: "IssuedAt",
                table: "Invoices",
                newName: "IssueDate");

            migrationBuilder.RenameColumn(
                name: "Balance",
                table: "Invoices",
                newName: "TotalPaid");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "ContactId",
                table: "Invoices",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceDue",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByFK",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CustomerContacts_ContactId",
                table: "Invoices",
                column: "ContactId",
                principalTable: "CustomerContacts",
                principalColumn: "ContactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CustomerContacts_ContactId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BalanceDue",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedByFK",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Invoices");

            migrationBuilder.RenameColumn(
                name: "AmountPaid",
                table: "Payments",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "VATType",
                table: "Invoices",
                newName: "PaymentStatus");

            migrationBuilder.RenameColumn(
                name: "TotalPaid",
                table: "Invoices",
                newName: "Balance");

            migrationBuilder.RenameColumn(
                name: "IssueDate",
                table: "Invoices",
                newName: "IssuedAt");

            migrationBuilder.AlterColumn<int>(
                name: "ContactId",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CustomerContacts_ContactId",
                table: "Invoices",
                column: "ContactId",
                principalTable: "CustomerContacts",
                principalColumn: "ContactId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

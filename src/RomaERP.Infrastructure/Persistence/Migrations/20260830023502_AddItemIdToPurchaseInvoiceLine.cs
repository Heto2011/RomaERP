using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemIdToPurchaseInvoiceLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                table: "PurchaseInvoiceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_ItemId",
                table: "PurchaseInvoiceLines",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLines_Items_ItemId",
                table: "PurchaseInvoiceLines",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLines_Items_ItemId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoiceLines_ItemId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "PurchaseInvoiceLines");
        }
    }
}

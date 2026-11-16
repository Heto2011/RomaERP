using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceLineVat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "SalesInvoiceLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "SalesInvoiceLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "PurchaseInvoiceLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "PurchaseInvoiceLines",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "SalesInvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "SalesInvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "PurchaseInvoiceLines");
        }
    }
}

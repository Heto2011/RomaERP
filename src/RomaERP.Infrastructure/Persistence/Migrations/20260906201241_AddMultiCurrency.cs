using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToFunctional",
                table: "SalesPayments",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "SalesInvoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToFunctional",
                table: "SalesInvoices",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToFunctional",
                table: "PurchasePayments",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "PurchaseInvoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToFunctional",
                table: "PurchaseInvoices",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            // Backfill every pre-existing invoice with this tenant's own functional currency — each
            // tenant has its own database, so this subquery always resolves to that tenant's setting.
            migrationBuilder.Sql(
                "UPDATE [SalesInvoices] SET [CurrencyCode] = COALESCE((SELECT TOP 1 [DefaultCurrency] FROM [CompanySettings]), 'EGP') WHERE [CurrencyCode] = '';");
            migrationBuilder.Sql(
                "UPDATE [PurchaseInvoices] SET [CurrencyCode] = COALESCE((SELECT TOP 1 [DefaultCurrency] FROM [CompanySettings]), 'EGP') WHERE [CurrencyCode] = '';");

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RateToFunctional = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CurrencyCode_RateDate",
                table: "ExchangeRates",
                columns: new[] { "CurrencyCode", "RateDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToFunctional",
                table: "SalesPayments");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToFunctional",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToFunctional",
                table: "PurchasePayments");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToFunctional",
                table: "PurchaseInvoices");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EInvoiceErrorMessage",
                table: "SalesInvoices",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoiceExternalUuid",
                table: "SalesInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoiceHash",
                table: "SalesInvoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EInvoiceStatus",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EInvoiceSubmittedAtUtc",
                table: "SalesInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingCertificateEncrypted",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingClientId",
                table: "CompanySettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingClientSecretEncrypted",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EInvoicingEnvironment",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingLastInvoiceHash",
                table: "CompanySettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingPrivateKeyEncrypted",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EInvoicingProvider",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EInvoicingSubmittedCount",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EInvoiceErrorMessage",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceExternalUuid",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceHash",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceStatus",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceSubmittedAtUtc",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "EInvoicingCertificateEncrypted",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingClientId",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingClientSecretEncrypted",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingEnvironment",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingLastInvoiceHash",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingPrivateKeyEncrypted",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingProvider",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingSubmittedCount",
                table: "CompanySettings");
        }
    }
}

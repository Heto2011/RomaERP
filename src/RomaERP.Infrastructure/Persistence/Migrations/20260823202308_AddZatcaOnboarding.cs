using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZatcaOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaAddress",
                table: "CompanySettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaBusinessCategory",
                table: "CompanySettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaComplianceRequestId",
                table: "CompanySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaCsrPem",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaDeviceSerialNumber",
                table: "CompanySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaInvoiceType",
                table: "CompanySettings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaModel",
                table: "CompanySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EInvoicingZatcaOnboardingStage",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaOrganizationIdentifier",
                table: "CompanySettings",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaOrganizationUnitName",
                table: "CompanySettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoicingZatcaSolutionName",
                table: "CompanySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaAddress",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaBusinessCategory",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaComplianceRequestId",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaCsrPem",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaDeviceSerialNumber",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaInvoiceType",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaModel",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaOnboardingStage",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaOrganizationIdentifier",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaOrganizationUnitName",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "EInvoicingZatcaSolutionName",
                table: "CompanySettings");
        }
    }
}

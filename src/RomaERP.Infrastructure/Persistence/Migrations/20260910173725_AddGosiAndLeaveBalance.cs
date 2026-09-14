using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGosiAndLeaveBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GosiEmployeeDeductionAmount",
                table: "PayrollRunLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiEmployerContributionAmount",
                table: "PayrollRunLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AnnualLeaveDaysPerYear",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 21);

            migrationBuilder.AddColumn<bool>(
                name: "IsSaudiNational",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiEmployeeRatePercent",
                table: "CompanySettings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 9.75m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiEmployerAnnuitiesRatePercent",
                table: "CompanySettings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 9.75m);

            migrationBuilder.AddColumn<decimal>(
                name: "GosiEmployerHazardsRatePercent",
                table: "CompanySettings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 2.0m);

            migrationBuilder.AddColumn<bool>(
                name: "GosiEnabled",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GosiEmployeeDeductionAmount",
                table: "PayrollRunLines");

            migrationBuilder.DropColumn(
                name: "GosiEmployerContributionAmount",
                table: "PayrollRunLines");

            migrationBuilder.DropColumn(
                name: "AnnualLeaveDaysPerYear",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsSaudiNational",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GosiEmployeeRatePercent",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "GosiEmployerAnnuitiesRatePercent",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "GosiEmployerHazardsRatePercent",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "GosiEnabled",
                table: "CompanySettings");
        }
    }
}

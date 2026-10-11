using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261012120000_AddUkPayroll")]
    public partial class AddUkPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "UkNationalInsuranceNumber", table: "Employees", type: "nvarchar(13)", maxLength: 13, nullable: true);
            migrationBuilder.AddColumn<string>(name: "UkTaxCode", table: "Employees", type: "nvarchar(12)", maxLength: 12, nullable: true);
            migrationBuilder.AddColumn<string>(name: "UkNiCategory", table: "Employees", type: "nvarchar(1)", maxLength: 1, nullable: true);
            migrationBuilder.AddColumn<int>(name: "UkStudentLoanPlan", table: "Employees", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "UkPostgraduateLoan", table: "Employees", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "UkPensionEnrolled", table: "Employees", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<decimal>(name: "UkPensionEmployeePercent", table: "Employees", type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 5m);
            migrationBuilder.AddColumn<decimal>(name: "UkPensionEmployerPercent", table: "Employees", type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 3m);

            foreach (var column in new[] { "UkTaxablePay", "UkIncomeTax", "UkEmployeeNi", "UkEmployerNi", "UkStudentLoan", "UkPostgraduateLoan", "UkPensionEmployee", "UkPensionEmployer" })
            {
                migrationBuilder.AddColumn<decimal>(name: column, table: "PayrollRunLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "UkNationalInsuranceNumber", "UkTaxCode", "UkNiCategory", "UkStudentLoanPlan", "UkPostgraduateLoan", "UkPensionEnrolled", "UkPensionEmployeePercent", "UkPensionEmployerPercent" })
                migrationBuilder.DropColumn(name: column, table: "Employees");
            foreach (var column in new[] { "UkTaxablePay", "UkIncomeTax", "UkEmployeeNi", "UkEmployerNi", "UkStudentLoan", "UkPostgraduateLoan", "UkPensionEmployee", "UkPensionEmployer" })
                migrationBuilder.DropColumn(name: column, table: "PayrollRunLines");
        }
    }
}

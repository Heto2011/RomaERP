using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261013120000_AddEgyptPayroll")]
    public partial class AddEgyptPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "EgEmployeeInsurance", "EgEmployerInsurance", "EgIncomeTax" })
                migrationBuilder.AddColumn<decimal>(name: column, table: "PayrollRunLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "EgEmployeeInsurance", "EgEmployerInsurance", "EgIncomeTax" })
                migrationBuilder.DropColumn(name: column, table: "PayrollRunLines");
        }
    }
}

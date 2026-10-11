using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261014120000_AddGuernseyPayroll")]
    public partial class AddGuernseyPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "GgIncomeTaxRatePercent", table: "CompanySettings", type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 20m);
            migrationBuilder.AddColumn<decimal>(name: "GgPersonalAllowanceAnnual", table: "CompanySettings", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 15200m);
            migrationBuilder.AddColumn<decimal>(name: "GgEmployeeSocialRatePercent", table: "CompanySettings", type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 7.5m);
            migrationBuilder.AddColumn<decimal>(name: "GgEmployerSocialRatePercent", table: "CompanySettings", type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 7.1m);
            migrationBuilder.AddColumn<decimal>(name: "GgSocialMonthlyUpperLimit", table: "CompanySettings", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 15717m);

            foreach (var column in new[] { "GgIncomeTax", "GgEmployeeSocial", "GgEmployerSocial" })
                migrationBuilder.AddColumn<decimal>(name: column, table: "PayrollRunLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "GgIncomeTaxRatePercent", "GgPersonalAllowanceAnnual", "GgEmployeeSocialRatePercent", "GgEmployerSocialRatePercent", "GgSocialMonthlyUpperLimit" })
                migrationBuilder.DropColumn(name: column, table: "CompanySettings");
            foreach (var column in new[] { "GgIncomeTax", "GgEmployeeSocial", "GgEmployerSocial" })
                migrationBuilder.DropColumn(name: column, table: "PayrollRunLines");
        }
    }
}

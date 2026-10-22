using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.CentralMigrations
{
    /// <inheritdoc />
    public partial class AddTenantBusinessTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessTypes",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessTypes",
                table: "Tenants");
        }
    }
}

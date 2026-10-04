using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004120000_AddEmployeeNationality")]
    public partial class AddEmployeeNationality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Employees",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            // Employees already flagged as Saudi keep that nationality; the rest stay blank until someone picks one.
            migrationBuilder.Sql("UPDATE [Employees] SET [Nationality] = 'SA' WHERE [IsSaudiNational] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Employees");
        }
    }
}

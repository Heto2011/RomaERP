using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261017120000_AddItemLeadTime")]
    public partial class AddItemLeadTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "LeadTimeDays", table: "Items", type: "int", nullable: false, defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LeadTimeDays", table: "Items");
        }
    }
}

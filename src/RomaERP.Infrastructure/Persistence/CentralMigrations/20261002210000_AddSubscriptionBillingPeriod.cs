using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence.Central;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.CentralMigrations
{
    /// <inheritdoc />
    [DbContext(typeof(CentralDbContext))]
    [Migration("20261002210000_AddSubscriptionBillingPeriod")]
    public partial class AddSubscriptionBillingPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingPeriod",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "Subscriptions");
        }
    }
}

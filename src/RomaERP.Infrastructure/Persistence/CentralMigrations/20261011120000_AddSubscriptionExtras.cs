using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence.Central;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.CentralMigrations
{
    /// <inheritdoc />
    [DbContext(typeof(CentralDbContext))]
    [Migration("20261011120000_AddSubscriptionExtras")]
    public partial class AddSubscriptionExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ExtraBranchesPaid", table: "Subscriptions", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ExtraUsersPaid", table: "Subscriptions", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "ExtraBranchesLemonSubscriptionId", table: "Subscriptions", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ExtraUsersLemonSubscriptionId", table: "Subscriptions", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExtraBranchesPaid", table: "Subscriptions");
            migrationBuilder.DropColumn(name: "ExtraUsersPaid", table: "Subscriptions");
            migrationBuilder.DropColumn(name: "ExtraBranchesLemonSubscriptionId", table: "Subscriptions");
            migrationBuilder.DropColumn(name: "ExtraUsersLemonSubscriptionId", table: "Subscriptions");
        }
    }
}

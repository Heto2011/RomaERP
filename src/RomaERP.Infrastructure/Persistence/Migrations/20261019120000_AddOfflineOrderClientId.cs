using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261019120000_AddOfflineOrderClientId")]
    public partial class AddOfflineOrderClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "OfflineClientId", table: "RestaurantOrders", type: "uniqueidentifier", nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OfflineClientId", table: "RestaurantOrders", column: "OfflineClientId",
                unique: true, filter: "[OfflineClientId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_RestaurantOrders_OfflineClientId", table: "RestaurantOrders");
            migrationBuilder.DropColumn(name: "OfflineClientId", table: "RestaurantOrders");
        }
    }
}

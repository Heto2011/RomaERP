using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261018120000_AddRestaurantLoyalty")]
    public partial class AddRestaurantLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PointsBalance = table.Column<int>(type: "int", nullable: false),
                    LifetimePointsEarned = table.Column<int>(type: "int", nullable: false),
                    VisitCount = table.Column<int>(type: "int", nullable: false),
                    TotalSpent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastVisitUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_LoyaltyMembers", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey("FK_LoyaltyTransactions_LoyaltyMembers_LoyaltyMemberId", x => x.LoyaltyMemberId, "LoyaltyMembers", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(name: "LoyaltyMemberId", table: "RestaurantOrders", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<int>(name: "LoyaltyPointsToRedeem", table: "RestaurantOrders", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(name: "LoyaltyDiscountAmount", table: "RestaurantOrders", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(name: "LoyaltyPointsEarned", table: "RestaurantOrders", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<bool>(name: "LoyaltyEnabled", table: "CompanySettings", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<decimal>(name: "LoyaltySpendPerPoint", table: "CompanySettings", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 10m);
            migrationBuilder.AddColumn<decimal>(name: "LoyaltyPointValue", table: "CompanySettings", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0.5m);
            migrationBuilder.AddColumn<int>(name: "LoyaltyMinRedeemPoints", table: "CompanySettings", type: "int", nullable: false, defaultValue: 20);

            migrationBuilder.CreateIndex(name: "IX_LoyaltyMembers_Phone", table: "LoyaltyMembers", column: "Phone", unique: true);
            migrationBuilder.CreateIndex(name: "IX_LoyaltyTransactions_LoyaltyMemberId", table: "LoyaltyTransactions", column: "LoyaltyMemberId");
            migrationBuilder.CreateIndex(name: "IX_LoyaltyTransactions_RestaurantOrderId", table: "LoyaltyTransactions", column: "RestaurantOrderId");
            migrationBuilder.CreateIndex(name: "IX_RestaurantOrders_LoyaltyMemberId", table: "RestaurantOrders", column: "LoyaltyMemberId");
            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantOrders_LoyaltyMembers_LoyaltyMemberId", table: "RestaurantOrders", column: "LoyaltyMemberId",
                principalTable: "LoyaltyMembers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_RestaurantOrders_LoyaltyMembers_LoyaltyMemberId", table: "RestaurantOrders");
            migrationBuilder.DropIndex(name: "IX_RestaurantOrders_LoyaltyMemberId", table: "RestaurantOrders");
            foreach (var column in new[] { "LoyaltyMemberId", "LoyaltyPointsToRedeem", "LoyaltyDiscountAmount", "LoyaltyPointsEarned" })
                migrationBuilder.DropColumn(name: column, table: "RestaurantOrders");
            foreach (var column in new[] { "LoyaltyEnabled", "LoyaltySpendPerPoint", "LoyaltyPointValue", "LoyaltyMinRedeemPoints" })
                migrationBuilder.DropColumn(name: column, table: "CompanySettings");
            migrationBuilder.DropTable(name: "LoyaltyTransactions");
            migrationBuilder.DropTable(name: "LoyaltyMembers");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountsLinkedToBranchAndAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StockAdjustmentId",
                table: "PhysicalStockCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "PhysicalStockCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalStockCounts_WarehouseId",
                table: "PhysicalStockCounts",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalStockCounts_Warehouses_WarehouseId",
                table: "PhysicalStockCounts",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalStockCounts_Warehouses_WarehouseId",
                table: "PhysicalStockCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalStockCounts_WarehouseId",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "StockAdjustmentId",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "PhysicalStockCounts");
        }
    }
}

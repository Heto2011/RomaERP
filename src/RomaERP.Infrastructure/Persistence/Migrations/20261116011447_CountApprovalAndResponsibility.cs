using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountApprovalAndResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Responsibility",
                table: "StockAdjustments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleEmployeeId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                table: "PhysicalStockCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedBy",
                table: "PhysicalStockCounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "PhysicalStockCounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Responsibility",
                table: "PhysicalStockCounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleEmployeeId",
                table: "PhysicalStockCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PhysicalStockCounts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Responsibility",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ResponsibleEmployeeId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "DecidedBy",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "Responsibility",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "ResponsibleEmployeeId",
                table: "PhysicalStockCounts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PhysicalStockCounts");
        }
    }
}

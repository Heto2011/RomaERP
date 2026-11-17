using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedCostAllocationLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCostAllocation",
                table: "JournalEntryLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "JournalEntryLines",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCostAllocation",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "JournalEntryLines");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManufacturingBoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutputItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingBoms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingBoms_Items_OutputItemId",
                        column: x => x.OutputItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManufacturingBomLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityPerBatch = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingBomLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingBomLines_Items_RawMaterialItemId",
                        column: x => x.RawMaterialItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManufacturingBomLines_ManufacturingBoms_BomId",
                        column: x => x.BomId,
                        principalTable: "ManufacturingBoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManufacturingOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProducedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingOrders_ManufacturingBoms_BomId",
                        column: x => x.BomId,
                        principalTable: "ManufacturingBoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManufacturingOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManufacturingOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManufacturingOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityConsumed = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingOrderLines_Items_RawMaterialItemId",
                        column: x => x.RawMaterialItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManufacturingOrderLines_ManufacturingOrders_ManufacturingOrderId",
                        column: x => x.ManufacturingOrderId,
                        principalTable: "ManufacturingOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingBomLines_BomId",
                table: "ManufacturingBomLines",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingBomLines_RawMaterialItemId",
                table: "ManufacturingBomLines",
                column: "RawMaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingBoms_OutputItemId",
                table: "ManufacturingBoms",
                column: "OutputItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingOrderLines_ManufacturingOrderId",
                table: "ManufacturingOrderLines",
                column: "ManufacturingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingOrderLines_RawMaterialItemId",
                table: "ManufacturingOrderLines",
                column: "RawMaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingOrders_BomId",
                table: "ManufacturingOrders",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingOrders_OrderNumber",
                table: "ManufacturingOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingOrders_WarehouseId",
                table: "ManufacturingOrders",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManufacturingBomLines");

            migrationBuilder.DropTable(
                name: "ManufacturingOrderLines");

            migrationBuilder.DropTable(
                name: "ManufacturingOrders");

            migrationBuilder.DropTable(
                name: "ManufacturingBoms");
        }
    }
}

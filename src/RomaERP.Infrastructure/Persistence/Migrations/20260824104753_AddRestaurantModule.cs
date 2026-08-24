using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMenuItem",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MenuPrice",
                table: "Items",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MenuRecipeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityPerUnit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuRecipeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuRecipeLines_Items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuRecipeLines_Items_RawMaterialItemId",
                        column: x => x.RawMaterialItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderType = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WaiterEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SalesInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_Employees_WaiterEmployeeId",
                        column: x => x.WaiterEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_RestaurantTables_TableId",
                        column: x => x.TableId,
                        principalTable: "RestaurantTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalTable: "RestaurantOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuRecipeLines_MenuItemId_RawMaterialItemId",
                table: "MenuRecipeLines",
                columns: new[] { "MenuItemId", "RawMaterialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuRecipeLines_RawMaterialItemId",
                table: "MenuRecipeLines",
                column: "RawMaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_ItemId",
                table: "RestaurantOrderLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_RestaurantOrderId",
                table: "RestaurantOrderLines",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrderNumber",
                table: "RestaurantOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_SalesInvoiceId",
                table: "RestaurantOrders",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_TableId",
                table: "RestaurantOrders",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_WaiterEmployeeId",
                table: "RestaurantOrders",
                column: "WaiterEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_WarehouseId",
                table: "RestaurantOrders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_Number",
                table: "RestaurantTables",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuRecipeLines");

            migrationBuilder.DropTable(
                name: "RestaurantOrderLines");

            migrationBuilder.DropTable(
                name: "RestaurantOrders");

            migrationBuilder.DropTable(
                name: "RestaurantTables");

            migrationBuilder.DropColumn(
                name: "IsMenuItem",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MenuPrice",
                table: "Items");
        }
    }
}

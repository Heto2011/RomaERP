using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QrOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QrOrderRadiusMeters",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "QrOrderWarehouseId",
                table: "CompanySettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrOrderingEnabled",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QrSecret",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QrSecretChangedAtUtc",
                table: "CompanySettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RestaurantLatitude",
                table: "CompanySettings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RestaurantLongitude",
                table: "CompanySettings",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QrOrderRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DistanceMeters = table.Column<int>(type: "int", nullable: false),
                    DeviceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RestaurantOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrOrderRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QrOrderRequestLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QrOrderRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrOrderRequestLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrOrderRequestLine_QrOrderRequests_QrOrderRequestId",
                        column: x => x.QrOrderRequestId,
                        principalTable: "QrOrderRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QrOrderRequestLine_QrOrderRequestId",
                table: "QrOrderRequestLine",
                column: "QrOrderRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_QrOrderRequests_Status_CreatedAtUtc",
                table: "QrOrderRequests",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QrOrderRequestLine");

            migrationBuilder.DropTable(
                name: "QrOrderRequests");

            migrationBuilder.DropColumn(
                name: "QrOrderRadiusMeters",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QrOrderWarehouseId",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QrOrderingEnabled",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QrSecret",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QrSecretChangedAtUtc",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "RestaurantLatitude",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "RestaurantLongitude",
                table: "CompanySettings");
        }
    }
}

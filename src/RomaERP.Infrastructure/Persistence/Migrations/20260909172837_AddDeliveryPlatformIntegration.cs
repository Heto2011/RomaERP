using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPlatformIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderRef",
                table: "RestaurantOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlatform",
                table: "RestaurantOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryPlatformItemMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalItemId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPlatformItemMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryPlatformItemMappings_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsSignatureVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryWebhookEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryWebhookEvents_RestaurantOrders_CreatedOrderId",
                        column: x => x.CreatedOrderId,
                        principalTable: "RestaurantOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_SourcePlatform_ExternalOrderRef",
                table: "RestaurantOrders",
                columns: new[] { "SourcePlatform", "ExternalOrderRef" },
                unique: true,
                filter: "[SourcePlatform] IS NOT NULL AND [ExternalOrderRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPlatformItemMappings_ItemId",
                table: "DeliveryPlatformItemMappings",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPlatformItemMappings_PlatformName_ExternalItemId",
                table: "DeliveryPlatformItemMappings",
                columns: new[] { "PlatformName", "ExternalItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryWebhookEvents_CreatedOrderId",
                table: "DeliveryWebhookEvents",
                column: "CreatedOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryPlatformItemMappings");

            migrationBuilder.DropTable(
                name: "DeliveryWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantOrders_SourcePlatform_ExternalOrderRef",
                table: "RestaurantOrders");

            migrationBuilder.DropColumn(
                name: "ExternalOrderRef",
                table: "RestaurantOrders");

            migrationBuilder.DropColumn(
                name: "SourcePlatform",
                table: "RestaurantOrders");
        }
    }
}

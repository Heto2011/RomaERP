using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemIsOpenPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOpenPrice",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOpenPrice",
                table: "Items");
        }
    }
}

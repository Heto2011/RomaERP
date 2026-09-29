using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustodyFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustodyEmployeeId",
                table: "ExpenseCaptures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FundingSource",
                table: "ExpenseCaptures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CustodyBalance",
                table: "Employees",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCaptures_CustodyEmployeeId",
                table: "ExpenseCaptures",
                column: "CustodyEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseCaptures_Employees_CustodyEmployeeId",
                table: "ExpenseCaptures",
                column: "CustodyEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseCaptures_Employees_CustodyEmployeeId",
                table: "ExpenseCaptures");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCaptures_CustodyEmployeeId",
                table: "ExpenseCaptures");

            migrationBuilder.DropColumn(
                name: "CustodyEmployeeId",
                table: "ExpenseCaptures");

            migrationBuilder.DropColumn(
                name: "FundingSource",
                table: "ExpenseCaptures");

            migrationBuilder.DropColumn(
                name: "CustodyBalance",
                table: "Employees");
        }
    }
}

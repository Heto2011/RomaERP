using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261022150000_AddExpensePolicies")]
    public partial class AddExpensePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpensePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RequireProofAbove = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BlocksApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpensePolicies", x => x.Id);
                    table.ForeignKey("FK_ExpensePolicies_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_ExpensePolicies_AccountId", table: "ExpensePolicies", column: "AccountId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExpensePolicies");
        }
    }
}

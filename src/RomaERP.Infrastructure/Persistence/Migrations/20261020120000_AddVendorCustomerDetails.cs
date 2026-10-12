using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261020120000_AddVendorCustomerDetails")]
    public partial class AddVendorCustomerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ContactPerson", table: "Vendors", type: "nvarchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Mobile", table: "Vendors", type: "nvarchar(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Address", table: "Vendors", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>(name: "City", table: "Vendors", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "CommercialRegisterNumber", table: "Vendors", type: "nvarchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Notes", table: "Vendors", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<int>(name: "PaymentTermsDays", table: "Vendors", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(name: "CreditLimit", table: "Vendors", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<string>(name: "BankName", table: "Vendors", type: "nvarchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>(name: "BankAccountNumber", table: "Vendors", type: "nvarchar(60)", maxLength: 60, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ContactPerson", table: "Customers", type: "nvarchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Mobile", table: "Customers", type: "nvarchar(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Address", table: "Customers", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>(name: "City", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "CommercialRegisterNumber", table: "Customers", type: "nvarchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Notes", table: "Customers", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<int>(name: "PaymentTermsDays", table: "Customers", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(name: "CreditLimit", table: "Customers", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "ContactPerson", "Mobile", "Address", "City", "CommercialRegisterNumber", "Notes", "PaymentTermsDays", "CreditLimit", "BankName", "BankAccountNumber" })
                migrationBuilder.DropColumn(name: column, table: "Vendors");
            foreach (var column in new[] { "ContactPerson", "Mobile", "Address", "City", "CommercialRegisterNumber", "Notes", "PaymentTermsDays", "CreditLimit" })
                migrationBuilder.DropColumn(name: column, table: "Customers");
        }
    }
}

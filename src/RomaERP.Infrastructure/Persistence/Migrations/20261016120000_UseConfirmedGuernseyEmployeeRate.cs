using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RomaERP.Infrastructure.Persistence;

#nullable disable

namespace RomaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016120000_UseConfirmedGuernseyEmployeeRate")]
    public partial class UseConfirmedGuernseyEmployeeRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Guernsey employee social security rate was confirmed as 7%; move companies still on the earlier 7.5% placeholder.
            migrationBuilder.Sql("UPDATE [CompanySettings] SET [GgEmployeeSocialRatePercent] = 7 WHERE [GgEmployeeSocialRatePercent] = 7.5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class ProductScopeAccessTests
{
    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/restaurant/orders")]
    [InlineData("/api/journalentries")]
    [InlineData("/api/aiassistant/ask")]
    [InlineData("/api/einvoicing/submit")]
    [InlineData("/api/sales/invoices")]
    public void FullProduct_IsNeverBlocked(string path) => Assert.True(ProductScopeAccess.IsAllowed(ProductScope.Full, path));

    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/payroll/runs")]
    [InlineData("/api/restaurant/orders")]
    [InlineData("/api/restaurant/loyalty/settings")]
    [InlineData("/api/cashiershifts")]
    [InlineData("/api/inventory/movements")]
    [InlineData("/api/aiassistant/ask")]
    [InlineData("/api/einvoicing/submit")]
    public void FreeBooks_CannotReachHrRestaurantStockAiOrEInvoicing(string path) => Assert.False(ProductScopeAccess.IsAllowed(ProductScope.BooksOnly, path));

    [Theory]
    [InlineData("/api/journalentries")]
    [InlineData("/api/loans")]
    [InlineData("/api/deferrals")]
    [InlineData("/api/accounts")]
    [InlineData("/api/financialreports/income-statement")]
    [InlineData("/api/sales/invoices")]
    [InlineData("/api/customer-followups/overdue")]
    [InlineData("/api/purchasing/invoices")]
    [InlineData("/api/items")]          // invoice lines pick items
    [InlineData("/api/warehouses")]
    [InlineData("/api/users")]
    [InlineData("/api/my-subscription")]
    [InlineData("/api/lookups/fiscal-periods")]
    public void FreeBooks_KeepsAccountingSalesPurchasingAndTheSharedScreens(string path) => Assert.True(ProductScopeAccess.IsAllowed(ProductScope.BooksOnly, path));

    [Theory]
    [InlineData("/api/restaurant/orders")]
    [InlineData("/api/cashiershifts")]
    [InlineData("/api/inventory/movements")]
    [InlineData("/api/items")]
    [InlineData("/api/purchasing/invoices")]
    [InlineData("/api/financialreports/income-statement")]
    [InlineData("/api/sales/invoices")]
    [InlineData("/api/aiassistant/ask")]
    [InlineData("/api/recurring-expenses")]
    public void RomaPos_KeepsPosStockPurchasingAndReports(string path) => Assert.True(ProductScopeAccess.IsAllowed(ProductScope.PosOnly, path));

    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/attendance")]
    [InlineData("/api/journalentries")]
    [InlineData("/api/fixedassets")]
    [InlineData("/api/loans")]
    public void RomaPos_CannotReachHrOrTheJournalScreens(string path) => Assert.False(ProductScopeAccess.IsAllowed(ProductScope.PosOnly, path));

    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/payroll/runs")]
    [InlineData("/api/accounts")]       // payroll components link to an account
    [InlineData("/api/users")]
    public void RomaHr_KeepsHrAndTheSharedScreens(string path) => Assert.True(ProductScopeAccess.IsAllowed(ProductScope.PeopleOnly, path));

    [Theory]
    [InlineData("/api/sales/invoices")]
    [InlineData("/api/purchasing/invoices")]
    [InlineData("/api/restaurant/orders")]
    [InlineData("/api/items")]
    [InlineData("/api/journalentries")]
    [InlineData("/api/aiassistant/ask")]
    public void RomaHr_CannotReachSalesPurchasingRestaurantOrStock(string path) => Assert.False(ProductScopeAccess.IsAllowed(ProductScope.PeopleOnly, path));

    [Fact]
    public void UnknownRoutes_AreOpen_SoANewSharedScreenIsNeverLockedOutByAccident()
    {
        Assert.True(ProductScopeAccess.IsAllowed(ProductScope.BooksOnly, "/api/something-new"));
        Assert.Equal(ProductScopeAccess.Area.Common, ProductScopeAccess.AreaOf("/api/auth/login"));
        Assert.Equal(ProductScopeAccess.Area.Restaurant, ProductScopeAccess.AreaOf("/api/Restaurant/Orders"));
    }
}

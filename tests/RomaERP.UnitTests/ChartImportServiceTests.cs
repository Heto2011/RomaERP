using System.Text;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using Xunit;

namespace RomaERP.UnitTests;

public class ChartImportServiceTests
{
    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Csv_IsReadIntoEditableLines_WithTypeAndParentWorkedOutFromTheCode()
    {
        var (ctx, _, _, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new ChartImportService(ctx);
        var csv = "كود الحساب,اسم الحساب\n1000,الأصول\n1100,الأصول المتداولة\n1110,Cash and banks\n1111,الصندوق\n5300,مصروفات عمومية\n";

        var preview = await service.PreviewFileAsync(Csv(csv), "chart.csv");

        Assert.True(preview.CanApply);
        var rows = preview.Rows.ToDictionary(r => r.Code);
        Assert.Equal(5, rows.Count);
        Assert.Equal(AccountType.Asset, rows["1111"].AccountType);
        Assert.Equal(AccountType.Expense, rows["5300"].AccountType);
        Assert.Equal("1110", rows["1111"].ParentCode);                       // 1111 -> 1110
        Assert.Equal("1100", rows["1110"].ParentCode);
        Assert.True(rows["1100"].IsControlAccount);
        Assert.False(rows["1111"].IsControlAccount);
        Assert.Equal("Cash and banks", rows["1110"].NameEn);                // a Latin name goes to the English name
        Assert.Equal("الصندوق", rows["1111"].NameAr);
    }

    [Fact]
    public async Task Preview_BlocksApproval_OnDuplicateCodesUnknownParentsAndTypeMismatch()
    {
        var (ctx, _, _, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new ChartImportService(ctx);
        var rows = new List<ChartImportRowDto>
        {
            new() { Code = "1000", NameAr = "أصول", AccountType = AccountType.Asset },
            new() { Code = "1000", NameAr = "مكرر", AccountType = AccountType.Asset },
            new() { Code = "2100", NameAr = "خصوم", AccountType = AccountType.Liability, ParentCode = "9999" },
            new() { Code = "1200", NameAr = "فرعي", AccountType = AccountType.Liability, ParentCode = "1000" },
        };

        var preview = await service.PreviewRowsAsync(rows);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("مكرر"));
        Assert.Contains(preview.Rows[2].Errors, e => e.Contains("9999"));
        Assert.Contains(preview.Rows[3].Errors, e => e.Contains("نفس نوع"));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ApplyAsync(rows));
    }

    [Fact]
    public async Task Apply_ReplacesTheChart_KeepsAccountsWithEntriesSwitchedOff_AndKeepsWhatTheSystemNeeds()
    {
        var (ctx, cash, fuelExpense, custody, period) = await ExpenseAssistantServiceTests.SeedAsync();
        var unused = new Account { Code = "6999", NameAr = "قديم بلا حركة", NameEn = "Old unused", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var withEntries = new Account { Code = "6998", NameAr = "قديم عليه قيود", NameEn = "Old used", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        ctx.Accounts.AddRange(unused, withEntries);
        await ctx.SaveChangesAsync();
        // one posted entry touches the old account, so history must survive
        await SimpleJournalEntryFactory.CreatePostedAsync(ctx, DateTime.UtcNow.Date, period.Id, "x", withEntries.Id, cash.Id, 100m, "T", CancellationToken.None);
        await ctx.SaveChangesAsync();

        var service = new ChartImportService(ctx);
        var rows = new List<ChartImportRowDto>
        {
            new() { Code = "1000", NameAr = "الأصول", NameEn = "Assets", AccountType = AccountType.Asset },
            new() { Code = "1111", NameAr = "الصندوق الجديد", NameEn = "Cash new", AccountType = AccountType.Asset, ParentCode = "1000" },
            new() { Code = "7000", NameAr = "حساب جديد", NameEn = "New", AccountType = AccountType.Asset, ParentCode = "1000" },
        };

        var preview = await service.PreviewRowsAsync(rows);
        Assert.True(preview.CanApply);
        Assert.Equal(2, preview.Summary.NewAccounts);                 // 1000 and 7000
        Assert.Equal(1, preview.Summary.UpdatedAccounts);             // 1111
        Assert.Equal(1, preview.Summary.RemovedAccounts);             // 6999 (nothing uses it)
        Assert.Equal(1, preview.Summary.DeactivatedAccounts);         // 6998 (has an entry)
        Assert.Equal(2, preview.Summary.KeptForSystem.Count);         // 5300 and 1170 are posted to by the system
        Assert.Equal(0, await ctx.Accounts.CountAsync(a => a.Code == "7000"));   // nothing saved by a preview

        var result = await service.ApplyAsync(rows);

        Assert.Equal(1, result.Summary.RemovedAccounts);
        var all = await ctx.Accounts.IgnoreQueryFilters().ToDictionaryAsync(a => a.Code);
        Assert.Equal("الصندوق الجديد", all["1111"].NameAr);            // same account, new name: its history stays attached
        Assert.Equal(cash.Id, all["1111"].Id);
        Assert.Equal(all["1000"].Id, all["1111"].ParentAccountId);
        Assert.Equal(2, all["1111"].Level);
        Assert.True(all["1000"].IsControlAccount);
        Assert.True(all["6999"].IsDeleted);                             // unused: removed
        Assert.False(all["6998"].IsDeleted);                            // has entries: stays, switched off
        Assert.False(all["6998"].IsActive);
        Assert.True(all["5300"].IsActive);                              // needed by the system: untouched
        Assert.True(all["1170"].IsActive);
    }
}

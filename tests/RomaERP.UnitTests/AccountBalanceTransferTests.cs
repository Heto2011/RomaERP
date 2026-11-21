using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using Xunit;

namespace RomaERP.UnitTests;

public class AccountBalanceTransferTests
{
    [Fact]
    public async Task DebitBalance_MovesToTheNewAccount_WithOneEntry_AndTheOldOneEndsAtZero()
    {
        var (ctx, cash, expense, custody, period) = await ExpenseAssistantServiceTests.SeedAsync();
        var newCash = new Account { Code = "1115", NameAr = "الخزينة الجديدة", NameEn = "New cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        ctx.Accounts.Add(newCash);
        await ctx.SaveChangesAsync();
        // the old cash account holds 700 (debit): 1000 in, 300 out
        await SimpleJournalEntryFactory.CreatePostedAsync(ctx, DateTime.UtcNow.Date, period.Id, "in", cash.Id, custody.Id, 1000m, "T", CancellationToken.None);
        await SimpleJournalEntryFactory.CreatePostedAsync(ctx, DateTime.UtcNow.Date, period.Id, "out", expense.Id, cash.Id, 300m, "T", CancellationToken.None);
        await ctx.SaveChangesAsync();
        var service = new AccountBalanceTransferService(ctx);
        var request = new BalanceTransferRequestDto { FromAccountId = cash.Id, ToAccountId = newCash.Id, Date = DateTime.UtcNow.Date };

        var preview = await service.PreviewAsync(request);
        Assert.True(preview.CanApply);
        Assert.Equal(700m, preview.Balance);
        Assert.Contains("1115", preview.DebitAccount);                 // the new account takes the debit
        Assert.Contains("1111", preview.CreditAccount);
        Assert.Equal(0, await ctx.JournalEntries.CountAsync(e => e.Reference == "ACCOUNT-TRANSFER"));   // a preview posts nothing

        var result = await service.ApplyAsync(request);

        Assert.Equal(700m, result.Amount);
        Assert.Equal(1, await ctx.JournalEntries.CountAsync(e => e.Reference == "ACCOUNT-TRANSFER"));
        var after = await service.PreviewAsync(request);
        Assert.False(after.CanApply);                                   // the old account now stands at zero
        Assert.Equal(0m, after.Balance);
    }

    [Fact]
    public async Task Refuses_DifferentTypes_IncomeAndExpense_SameAccount_AndAParentAccount()
    {
        var (ctx, cash, expense, custody, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var parent = new Account { Code = "1100", NameAr = "متداولة", NameEn = "Current", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        ctx.Accounts.Add(parent);
        await ctx.SaveChangesAsync();
        custody.ParentAccountId = parent.Id;
        await ctx.SaveChangesAsync();
        var expense2 = new Account { Code = "5310", NameAr = "مصروف آخر", NameEn = "Other expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        ctx.Accounts.Add(expense2);
        await ctx.SaveChangesAsync();
        var service = new AccountBalanceTransferService(ctx);

        Assert.NotNull((await service.PreviewAsync(new() { FromAccountId = cash.Id, ToAccountId = cash.Id })).Blocked);
        Assert.Contains("نفس النوع", (await service.PreviewAsync(new() { FromAccountId = cash.Id, ToAccountId = expense.Id })).Blocked);
        Assert.Contains("المحاسب", (await service.PreviewAsync(new() { FromAccountId = expense.Id, ToAccountId = expense2.Id })).Blocked);      // income and expense are the accountant's call
        Assert.Contains("رئيسي", (await service.PreviewAsync(new() { FromAccountId = cash.Id, ToAccountId = parent.Id })).Blocked);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ApplyAsync(new() { FromAccountId = cash.Id, ToAccountId = cash.Id }));
    }
}

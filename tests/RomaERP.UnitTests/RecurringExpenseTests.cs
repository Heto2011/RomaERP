using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Assistant;
using Xunit;

namespace RomaERP.UnitTests;

public class RecurringExpenseTests
{
    private static RecurringExpenseDto Rent(Guid accountId, decimal? amount = 12000, int day = 5, PaymentMethod pay = PaymentMethod.Cash)
        => new() { Description = "إيجار المحل", AccountId = accountId, Amount = amount, DayOfMonth = day, PaymentMethod = pay };

    [Fact]
    public async Task Item_IsPreparedOncePerMonth_OnceItsDayHasCome_AndNeverPostsAnything()
    {
        var (ctx, _, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new RecurringExpenseService(ctx);
        await service.SaveAsync(null, Rent(expense.Id, day: 5));

        Assert.Equal(0, await service.PrepareDueAsync(new DateTime(2026, 10, 4), "u"));
        Assert.Equal(1, await service.PrepareDueAsync(new DateTime(2026, 10, 5), "u"));
        Assert.Equal(0, await service.PrepareDueAsync(new DateTime(2026, 10, 20), "u"));      // same month: not again
        Assert.Equal(1, await service.PrepareDueAsync(new DateTime(2026, 11, 7), "u"));       // next month

        var captures = await ctx.ExpenseCaptures.OrderBy(c => c.EntryDate).ToListAsync();
        Assert.Equal(2, captures.Count);
        Assert.All(captures, c => { Assert.Equal(ExpenseCaptureStatus.PendingApproval, c.Status); Assert.Equal(12000, c.Amount); Assert.Null(c.JournalEntryId); });
        Assert.Equal(new DateTime(2026, 10, 5), captures[0].EntryDate);
        Assert.Equal(0, await ctx.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task VariableAmount_IsPreparedWithoutAmount_AndCardWaitsForTheBank_AndInactiveIsSkipped()
    {
        var (ctx, _, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new RecurringExpenseService(ctx);
        await service.SaveAsync(null, new RecurringExpenseDto { Description = "كهرباء", AccountId = expense.Id, Amount = null, DayOfMonth = 1, PaymentMethod = PaymentMethod.Cash });
        await service.SaveAsync(null, new RecurringExpenseDto { Description = "اشتراك", AccountId = expense.Id, Amount = 500, DayOfMonth = 1, PaymentMethod = PaymentMethod.Card });
        var paused = await service.SaveAsync(null, new RecurringExpenseDto { Description = "موقوف", AccountId = expense.Id, Amount = 99, DayOfMonth = 1, PaymentMethod = PaymentMethod.Cash });
        await service.SetActiveAsync(paused.Id, false);

        Assert.Equal(2, await service.PrepareDueAsync(new DateTime(2026, 10, 2), "u"));

        var electricity = await ctx.ExpenseCaptures.SingleAsync(c => c.Description == "كهرباء");
        Assert.Null(electricity.Amount);
        Assert.Equal(ExpenseCaptureStatus.PendingApproval, electricity.Status);
        Assert.Equal(ExpenseCaptureStatus.AwaitingReconciliation, (await ctx.ExpenseCaptures.SingleAsync(c => c.Description == "اشتراك")).Status);
        Assert.False(await ctx.ExpenseCaptures.AnyAsync(c => c.Description == "موقوف"));
    }

    [Fact]
    public async Task MarkingACapturedExpense_CreatesTheItem_WithoutDoublingThisMonth()
    {
        var (ctx, _, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new RecurringExpenseService(ctx);
        var capture = new ExpenseCapture
        {
            RawText = "ايجار", Amount = 8000, Currency = "EGP", Description = "إيجار المخزن", EntryDate = new DateTime(2026, 10, 3),
            SuggestedAccountId = expense.Id, PaymentMethod = PaymentMethod.Cash, FundingSource = FundingSource.CompanyAccount,
            Status = ExpenseCaptureStatus.PendingApproval, SubmittedByUserId = "u"
        };
        ctx.ExpenseCaptures.Add(capture);
        await ctx.SaveChangesAsync();

        var item = await service.MarkFromCaptureAsync(capture.Id, new MarkRecurringDto { AmountIsFixed = true }, "u");
        Assert.Equal(8000, item.Amount);
        Assert.Equal(3, item.DayOfMonth);

        Assert.Equal(0, await service.PrepareDueAsync(new DateTime(2026, 10, 28), "u"));      // this month already has its expense
        Assert.Equal(1, await service.PrepareDueAsync(new DateTime(2026, 11, 3), "u"));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MarkFromCaptureAsync(capture.Id, new MarkRecurringDto(), "u"));
    }

    [Fact]
    public async Task MarkingWithChangingAmount_DropsTheAmount_AndFixedNeedsOne()
    {
        var (ctx, _, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new RecurringExpenseService(ctx);
        var capture = new ExpenseCapture { RawText = "x", Amount = 640, Description = "كهرباء", EntryDate = new DateTime(2026, 10, 9), SuggestedAccountId = expense.Id,
            PaymentMethod = PaymentMethod.Cash, Status = ExpenseCaptureStatus.PendingApproval, SubmittedByUserId = "u" };
        var noAmount = new ExpenseCapture { RawText = "y", Amount = null, Description = "مياه", EntryDate = new DateTime(2026, 10, 9), SuggestedAccountId = expense.Id,
            PaymentMethod = PaymentMethod.Cash, Status = ExpenseCaptureStatus.PendingApproval, SubmittedByUserId = "u" };
        ctx.ExpenseCaptures.AddRange(capture, noAmount);
        await ctx.SaveChangesAsync();

        Assert.Null((await service.MarkFromCaptureAsync(capture.Id, new MarkRecurringDto { AmountIsFixed = false }, "u")).Amount);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MarkFromCaptureAsync(noAmount.Id, new MarkRecurringDto { AmountIsFixed = true }, "u"));
    }

    [Fact]
    public async Task Save_RejectsBadDay_NonExpenseAccount_AndZeroAmount()
    {
        var (ctx, cash, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var service = new RecurringExpenseService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SaveAsync(null, Rent(expense.Id, day: 29)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SaveAsync(null, Rent(cash.Id)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SaveAsync(null, Rent(expense.Id, amount: 0)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SaveAsync(null, new RecurringExpenseDto { Description = " ", AccountId = expense.Id, Amount = 5 }));
    }

    [Fact]
    public async Task Approver_CanTypeTheMissingAmount_ThenApprovePosts_AndApprovingWithoutOneIsRefused()
    {
        var (ctx, _, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var recurring = new RecurringExpenseService(ctx);
        var assistant = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());
        // The seeded fiscal period covers today ± 15 days, so use today's own day of the month as the due day.
        var dueDay = Math.Min(DateTime.UtcNow.Day, 28);
        var today = DateTime.UtcNow.Date;
        await recurring.SaveAsync(null, new RecurringExpenseDto { Description = "كهرباء", AccountId = expense.Id, Amount = null, DayOfMonth = dueDay, PaymentMethod = PaymentMethod.Cash });
        await recurring.PrepareDueAsync(new DateTime(today.Year, today.Month, dueDay), "u");
        var pending = (await assistant.GetPendingApprovalAsync()).Single();

        await Assert.ThrowsAsync<ValidationAppException>(() => assistant.ApproveAsync(pending.Id));
        await Assert.ThrowsAsync<ValidationAppException>(() => assistant.SetPendingAmountAsync(pending.Id, 0));

        await assistant.SetPendingAmountAsync(pending.Id, 735.5m);
        var posted = await assistant.ApproveAsync(pending.Id);
        Assert.Equal(ExpenseCaptureStatus.Posted, posted.Status);
        Assert.Equal(735.5m, posted.Amount);
        Assert.NotNull(posted.JournalEntryId);
        await Assert.ThrowsAsync<ValidationAppException>(() => assistant.SetPendingAmountAsync(pending.Id, 800));   // already posted
    }
}

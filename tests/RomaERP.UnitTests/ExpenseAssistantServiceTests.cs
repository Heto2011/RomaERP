using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Stand-in for the Claude API call so the conversation/business logic can be tested without network access.</summary>
public class FakeClaudeExpenseParser : IClaudeExpenseParser
{
    public Func<string, string?, ExpenseExtractionResult> Handler { get; set; } =
        (_, _) => new ExpenseExtractionResult(100, "EGP", "مصروف تجريبي", "5300", false, null);

    public Task<ExpenseExtractionResult> ExtractAsync(string userMessage, string? priorContext, IReadOnlyList<ExpenseAccountCandidate> expenseAccounts, CancellationToken ct = default)
        => Task.FromResult(Handler(userMessage, priorContext));
}

public class ExpenseAssistantServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account cash, Account fuelExpense, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var fuelExpense = new Account { Code = "5300", NameAr = "مصروفات إدارية وعمومية", NameEn = "Admin Expenses", AccountType = AccountType.Expense, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        ctx.Accounts.AddRange(cash, fuelExpense);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        return (ctx, cash, fuelExpense, period);
    }

    [Fact]
    public async Task SendMessage_WithCashReply_WaitsForAdminApprovalBeforePosting()
    {
        var (ctx, cash, fuelExpense, _) = await SeedAsync();
        var parser = new FakeClaudeExpenseParser();
        var service = new ExpenseAssistantService(ctx, parser);

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingPaymentMethod, first.Status);

        var second = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "كاش" }, "user-1");

        Assert.Equal(ExpenseCaptureStatus.PendingApproval, second.Status);
        Assert.Null(second.Capture!.JournalEntryId);

        var approved = await service.ApproveAsync(first.CaptureId);

        Assert.Equal(ExpenseCaptureStatus.Posted, approved.Status);
        Assert.NotNull(approved.JournalEntryId);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == approved.JournalEntryId);
        Assert.Equal(100, entry.TotalDebit);
        Assert.Contains(entry.Lines, l => l.AccountId == fuelExpense.Id && l.Debit == 100);
        Assert.Contains(entry.Lines, l => l.AccountId == cash.Id && l.Credit == 100);
    }

    [Fact]
    public async Task Reject_OnPendingApprovalCapture_MarksRejectedWithoutPosting()
    {
        var (ctx, _, _, _) = await SeedAsync();
        var service = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "كاش" }, "user-1");

        var rejected = await service.RejectAsync(first.CaptureId);

        Assert.Equal(ExpenseCaptureStatus.Rejected, rejected.Status);
        Assert.Null(rejected.JournalEntryId);
        Assert.Empty(await ctx.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task SendMessage_WithCardReply_LeavesCaptureAwaitingReconciliation()
    {
        var (ctx, _, _, _) = await SeedAsync();
        var service = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        var second = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "شبكة" }, "user-1");

        Assert.Equal(ExpenseCaptureStatus.AwaitingReconciliation, second.Status);
        Assert.Null(second.Capture!.JournalEntryId);
    }

    [Fact]
    public async Task SendMessage_WhenAmountMissing_AsksForClarificationThenResolves()
    {
        var (ctx, _, _, _) = await SeedAsync();
        var parser = new FakeClaudeExpenseParser
        {
            Handler = (msg, prior) => msg.Contains("100")
                ? new ExpenseExtractionResult(100, "EGP", "بنزين", "5300", false, null)
                : new ExpenseExtractionResult(null, null, null, null, true, "ممكن تقولي المبلغ؟")
        };
        var service = new ExpenseAssistantService(ctx, parser);

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingDetails, first.Status);

        var second = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "100 جنيه" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingPaymentMethod, second.Status);
    }

    [Fact]
    public async Task BankReconciliation_AutoMatchesCardExpenseWithinWindow_ThenNeedsApprovalToPost()
    {
        var (ctx, cash, fuelExpense, period) = await SeedAsync();
        var assistant = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());
        var reconciliation = new BankReconciliationService(ctx);

        var bankAccount = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        ctx.Accounts.Add(bankAccount);
        await ctx.SaveChangesAsync();

        var first = await assistant.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        await assistant.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "شبكة" }, "user-1");

        var bankLineDate = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd");
        var csv = $"Date,Description,Amount\n{bankLineDate},POS PURCHASE FUEL,100\n";
        using var csvStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var import = await reconciliation.ImportAsync(csvStream, "statement.csv", bankAccount.Id, "user-1");

        Assert.Equal(1, import.MatchedCount);

        var matchedCapture = await ctx.ExpenseCaptures.FirstAsync(c => c.Id == first.CaptureId);
        Assert.Equal(ExpenseCaptureStatus.PendingApproval, matchedCapture.Status);
        Assert.Null(matchedCapture.JournalEntryId);

        var approved = await assistant.ApproveAsync(first.CaptureId);

        Assert.Equal(ExpenseCaptureStatus.Posted, approved.Status);
        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == approved.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == bankAccount.Id && l.Credit == 100);
        Assert.Contains(entry.Lines, l => l.AccountId == fuelExpense.Id && l.Debit == 100);
    }
}

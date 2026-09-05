using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Stand-in for the Claude API call so the conversation/business logic can be tested without network access.</summary>
public class FakeClaudeExpenseParser : IClaudeExpenseParser
{
    public Func<string, string?, ExpenseExtractionResult> Handler { get; set; } =
        (_, _) => new ExpenseExtractionResult(100, "EGP", "مصروف تجريبي", "5300", false, null);

    public Func<byte[], string, ExpenseExtractionResult> ImageHandler { get; set; } =
        (_, _) => new ExpenseExtractionResult(75, "EGP", "إيصال تجريبي", "5300", false, null, new DateTime(2026, 8, 20));

    public Task<ExpenseExtractionResult> ExtractAsync(string userMessage, string? priorContext, IReadOnlyList<ExpenseAccountCandidate> expenseAccounts, CancellationToken ct = default)
        => Task.FromResult(Handler(userMessage, priorContext));

    public Task<ExpenseExtractionResult> ExtractFromReceiptImageAsync(byte[] imageBytes, string mediaType, IReadOnlyList<ExpenseAccountCandidate> expenseAccounts, CancellationToken ct = default)
        => Task.FromResult(ImageHandler(imageBytes, mediaType));
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

    private static async Task<(ApplicationDbContext ctx, Account cash, Account fuelExpense, Account custody, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var fuelExpense = new Account { Code = "5300", NameAr = "مصروفات إدارية وعمومية", NameEn = "Admin Expenses", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var custody = new Account { Code = "1170", NameAr = "عهد الموظفين", NameEn = "Employee Custodies", AccountType = AccountType.Asset, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        ctx.Accounts.AddRange(cash, fuelExpense, custody);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        return (ctx, cash, fuelExpense, custody, period);
    }

    [Fact]
    public async Task SendMessage_WithCompanyAccountThenCash_WaitsForAdminApprovalBeforePosting()
    {
        var (ctx, cash, fuelExpense, _, _) = await SeedAsync();
        var parser = new FakeClaudeExpenseParser();
        var service = new ExpenseAssistantService(ctx, parser);

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingFundingSource, first.Status);

        var second = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "جاري" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingPaymentMethod, second.Status);

        var third = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "كاش" }, "user-1");

        Assert.Equal(ExpenseCaptureStatus.PendingApproval, third.Status);
        Assert.Null(third.Capture!.JournalEntryId);

        var approved = await service.ApproveAsync(first.CaptureId);

        Assert.Equal(ExpenseCaptureStatus.Posted, approved.Status);
        Assert.NotNull(approved.JournalEntryId);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == approved.JournalEntryId);
        Assert.Equal(100, entry.TotalDebit);
        Assert.Contains(entry.Lines, l => l.AccountId == fuelExpense.Id && l.Debit == 100);
        Assert.Contains(entry.Lines, l => l.AccountId == cash.Id && l.Credit == 100);
    }

    [Fact]
    public async Task StartFromReceiptImage_WithReadableAmount_JumpsStraightToFundingSourceQuestion()
    {
        var (ctx, cash, fuelExpense, _, _) = await SeedAsync();
        var receiptDate = DateTime.UtcNow.Date.AddDays(-5);
        var parser = new FakeClaudeExpenseParser
        {
            ImageHandler = (_, _) => new ExpenseExtractionResult(145, "EGP", "مطعم الشيف", "5300", false, null, receiptDate)
        };
        var service = new ExpenseAssistantService(ctx, parser);

        var first = await service.StartFromReceiptImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", "user-1");

        Assert.Equal(ExpenseCaptureStatus.AwaitingFundingSource, first.Status);
        Assert.Equal(145, first.Capture!.Amount);
        Assert.Equal("مطعم الشيف", first.Capture.Description);
        Assert.Equal(receiptDate, first.Capture.EntryDate);
        Assert.Equal("5300", first.Capture.SuggestedAccountCode);

        var second = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "جاري" }, "user-1");
        var third = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "كاش" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.PendingApproval, third.Status);

        var approved = await service.ApproveAsync(first.CaptureId);
        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == approved.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == fuelExpense.Id && l.Debit == 145);
        Assert.Contains(entry.Lines, l => l.AccountId == cash.Id && l.Credit == 145);
    }

    [Fact]
    public async Task StartFromReceiptImage_WhenAmountUnreadable_AsksForClarificationInstead()
    {
        var (ctx, _, _, _, _) = await SeedAsync();
        var parser = new FakeClaudeExpenseParser
        {
            ImageHandler = (_, _) => new ExpenseExtractionResult(null, null, null, null, true, "الصورة مش واضحة، تقدر تكتب المبلغ؟")
        };
        var service = new ExpenseAssistantService(ctx, parser);

        var first = await service.StartFromReceiptImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", "user-1");

        Assert.Equal(ExpenseCaptureStatus.AwaitingDetails, first.Status);
        Assert.Null(first.Capture!.Amount);
        Assert.Equal("الصورة مش واضحة، تقدر تكتب المبلغ؟", first.AssistantReply);
    }

    [Fact]
    public async Task Reject_OnPendingApprovalCapture_MarksRejectedWithoutPosting()
    {
        var (ctx, _, _, _, _) = await SeedAsync();
        var service = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "جاري" }, "user-1");
        await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "كاش" }, "user-1");

        var rejected = await service.RejectAsync(first.CaptureId);

        Assert.Equal(ExpenseCaptureStatus.Rejected, rejected.Status);
        Assert.Null(rejected.JournalEntryId);
        Assert.Empty(await ctx.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task SendMessage_WithCompanyAccountThenCard_LeavesCaptureAwaitingReconciliation()
    {
        var (ctx, _, _, _, _) = await SeedAsync();
        var service = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "جاري" }, "user-1");
        var third = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "شبكة" }, "user-1");

        Assert.Equal(ExpenseCaptureStatus.AwaitingReconciliation, third.Status);
        Assert.Null(third.Capture!.JournalEntryId);
    }

    [Fact]
    public async Task SendMessage_WhenAmountMissing_AsksForClarificationThenResolves()
    {
        var (ctx, _, _, _, _) = await SeedAsync();
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
        Assert.Equal(ExpenseCaptureStatus.AwaitingFundingSource, second.Status);
    }

    [Fact]
    public async Task SendMessage_WithCustodyFunding_ResolvesEmployeeAndPostsAgainstCustodyOnApproval()
    {
        var (ctx, _, fuelExpense, custody, _) = await SeedAsync();

        var department = new Department { Code = "DEP-1", NameAr = "المبيعات", NameEn = "Sales" };
        ctx.Departments.Add(department);
        await ctx.SaveChangesAsync();
        var position = new Position { Code = "POS-1", TitleAr = "مندوب", TitleEn = "Rep", DepartmentId = department.Id };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();

        var employee = new Employee
        {
            EmployeeCode = "EMP-1",
            FullNameAr = "أحمد علي",
            FullNameEn = "Ahmed Ali",
            HireDate = DateTime.UtcNow.Date,
            DepartmentId = department.Id,
            PositionId = position.Id,
            EmploymentStatus = EmploymentStatus.Active,
            CustodyBalance = 500
        };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var service = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());

        var first = await service.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        var second = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "عهدة" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingCustodyEmployee, second.Status);

        var third = await service.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "أحمد علي" }, "user-1");
        Assert.Equal(ExpenseCaptureStatus.PendingApproval, third.Status);
        Assert.Equal(employee.Id, third.Capture!.CustodyEmployeeId);

        var approved = await service.ApproveAsync(first.CaptureId);

        Assert.Equal(ExpenseCaptureStatus.Posted, approved.Status);
        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == approved.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == fuelExpense.Id && l.Debit == 100);
        Assert.Contains(entry.Lines, l => l.AccountId == custody.Id && l.Credit == 100);

        var updatedEmployee = await ctx.Employees.FirstAsync(e => e.Id == employee.Id);
        Assert.Equal(400, updatedEmployee.CustodyBalance);
    }

    [Fact]
    public async Task BankReconciliation_AutoMatchesCardExpenseWithinWindow_ThenNeedsApprovalToPost()
    {
        var (ctx, cash, fuelExpense, _, period) = await SeedAsync();
        var assistant = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());
        var reconciliation = new BankReconciliationService(ctx);

        var bankAccount = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        ctx.Accounts.Add(bankAccount);
        await ctx.SaveChangesAsync();

        var first = await assistant.SendMessageAsync(new ChatTurnRequestDto { Message = "اشتريت بنزين بـ100 جنيه" }, "user-1");
        await assistant.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "جاري" }, "user-1");
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

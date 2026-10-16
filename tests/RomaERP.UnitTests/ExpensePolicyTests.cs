using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ExpensePolicyTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void Evaluate_FlagsAmountOverCeilingAndMissingProof()
    {
        var accountId = Guid.NewGuid();
        var policies = new[]
        {
            new ExpensePolicy { Name = "حد عام", MaxAmount = 500, BlocksApproval = true },
            new ExpensePolicy { Name = "إيصال", RequireProofAbove = 100, BlocksApproval = false },
            new ExpensePolicy { Name = "حساب تاني", AccountId = Guid.NewGuid(), MaxAmount = 1, BlocksApproval = true },
            new ExpensePolicy { Name = "موقوفة", MaxAmount = 1, IsActive = false, BlocksApproval = true }
        };
        var capture = new ExpenseCapture { Amount = 600, SuggestedAccountId = accountId };

        var issues = ExpensePolicyRules.Evaluate(capture, policies);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.PolicyName == "حد عام" && i.Blocks);
        Assert.Contains(issues, i => i.PolicyName == "إيصال" && !i.Blocks);

        capture.ProofStoragePath = "x/y.jpg";
        Assert.Single(ExpensePolicyRules.Evaluate(capture, policies));
    }

    [Fact]
    public void Evaluate_NoAmount_NoIssues()
        => Assert.Empty(ExpensePolicyRules.Evaluate(new ExpenseCapture(), new[] { new ExpensePolicy { Name = "a", MaxAmount = 1 } }));

    [Fact]
    public async Task Save_RequiresAtLeastOneLimit()
    {
        var service = new ExpensePolicyService(CreateContext());
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SaveAsync(null, new ExpensePolicyDto { Name = "فاضية" }));
    }

    [Fact]
    public async Task Save_UpdateAndDelete_Work()
    {
        var ctx = CreateContext();
        var service = new ExpensePolicyService(ctx);
        var created = await service.SaveAsync(null, new ExpensePolicyDto { Name = "حد", MaxAmount = 1000, BlocksApproval = true });
        var updated = await service.SaveAsync(created.Id, new ExpensePolicyDto { Name = "حد جديد", MaxAmount = 2000 });
        Assert.Equal("حد جديد", updated.Name);
        Assert.Equal(2000, updated.MaxAmount);
        Assert.Single(await service.GetAllAsync());

        await service.DeleteAsync(created.Id);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task Approve_IsBlockedByBlockingPolicy_ButNotByWarnOnly()
    {
        var ctx = CreateContext();
        var expense = new Account { Code = "5300", NameAr = "مصروفات", NameEn = "Admin", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        ctx.Accounts.AddRange(expense, cash);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) });
        var policy = new ExpensePolicy { Name = "حد 50", MaxAmount = 50, BlocksApproval = true };
        ctx.ExpensePolicies.Add(policy);
        await ctx.SaveChangesAsync();

        var assistant = new ExpenseAssistantService(ctx, new FakeClaudeExpenseParser());
        var first = await assistant.SendMessageAsync(new ChatTurnRequestDto { Message = "بنزين 100" }, "u1");
        await assistant.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "جاري" }, "u1");
        var third = await assistant.SendMessageAsync(new ChatTurnRequestDto { CaptureId = first.CaptureId, Message = "كاش" }, "u1");
        Assert.NotEmpty(third.Capture!.PolicyIssues);

        await Assert.ThrowsAsync<ValidationAppException>(() => assistant.ApproveAsync(first.CaptureId));

        policy.BlocksApproval = false;
        await ctx.SaveChangesAsync();
        var approved = await assistant.ApproveAsync(first.CaptureId);
        Assert.Equal(ExpenseCaptureStatus.Posted, approved.Status);
    }
}

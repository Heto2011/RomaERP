using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class LoanServiceTests
{
    private static async Task<(ApplicationDbContext ctx, Account bank, Account loan)> SeedAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var loan = new Account { Code = "2210", NameAr = "قروض طويلة الأجل", NameEn = "Long-term loans", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var expenses = new Account { Code = "5000", NameAr = "المصروفات", NameEn = "Expenses", AccountType = AccountType.Expense, Nature = AccountNature.Debit, IsControlAccount = true };
        ctx.Accounts.AddRange(bank, loan, expenses);
        ctx.FiscalPeriods.Add(new FiscalPeriod { Name = "All", PeriodNumber = 1, StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2040, 12, 31) });
        await ctx.SaveChangesAsync();
        return (ctx, bank, loan);
    }

    [Fact]
    public async Task Schedule_IsALevelAnnuity_WhosePrincipalPartsAddUpToTheLoan()
    {
        var (ctx, _, _) = await SeedAsync();
        var svc = new LoanService(ctx);

        var schedule = svc.BuildSchedule(12000, 12, 12, new DateTime(2026, 12, 1));

        Assert.Equal(12, schedule.Count);
        Assert.Equal(12000, schedule.Sum(s => s.PrincipalPart));
        Assert.Equal(120, schedule[0].InterestPart);                      // 12000 x 1%
        Assert.True(schedule[0].InterestPart > schedule[11].InterestPart); // interest shrinks as the balance does
        Assert.InRange(schedule[0].Total, 1066m, 1067m);                  // the textbook payment for 12000 / 12% / 12 months is about 1066.19
        Assert.Equal(new DateTime(2027, 11, 1), schedule[11].DueDate);
    }

    [Fact]
    public async Task InterestFreeLoan_SplitsEvenly_AndTheLastInstallmentAbsorbsTheRounding()
    {
        var (ctx, _, _) = await SeedAsync();
        var schedule = new LoanService(ctx).BuildSchedule(1000, 0, 3, new DateTime(2026, 12, 1));

        Assert.All(schedule, s => Assert.Equal(0, s.InterestPart));
        Assert.Equal(1000, schedule.Sum(s => s.Total));
        Assert.Equal(333.34m, schedule[2].Total);
    }

    [Fact]
    public async Task CreateThenPay_PostsBalancedEntries_ReducesTheBalance_AndInsistsOnOrder()
    {
        var (ctx, bank, loanAccount) = await SeedAsync();
        var svc = new LoanService(ctx);

        var created = await svc.CreateAsync(new CreateLoanDto
        {
            Description = "قرض البنك", Principal = 6000, AnnualInterestPercent = 12, TermMonths = 6,
            DisbursementDate = new DateTime(2026, 11, 1), FirstInstallmentDate = new DateTime(2026, 12, 1),
            LoanAccountId = loanAccount.Id, BankAccountId = bank.Id
        });

        // The interest account is created on demand, and the money coming in is posted.
        Assert.NotNull(await ctx.Accounts.FirstOrDefaultAsync(a => a.Code == "5600"));
        var first = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync();
        Assert.Equal(bank.Id, first.Lines.Single(l => l.Debit > 0).AccountId);
        Assert.Equal(6000, first.TotalDebit);

        var second = created.Installments[1];
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.PayInstallmentAsync(second.Id, new PayInstallmentDto { PaymentDate = new DateTime(2027, 1, 1) }));   // out of order

        var paid = await svc.PayInstallmentAsync(created.Installments[0].Id, new PayInstallmentDto { PaymentDate = new DateTime(2026, 12, 1) });
        Assert.Equal(1, paid.PaidInstallments);
        Assert.Equal(6000 - created.Installments[0].PrincipalPart, paid.Remaining);
        var entries = await ctx.JournalEntries.Include(e => e.Lines).ToListAsync();
        Assert.All(entries, e => Assert.True(e.IsBalanced));
        var payment = entries.Last();
        Assert.Equal(created.Installments[0].Total, payment.Lines.Single(l => l.AccountId == bank.Id).Credit);
        Assert.Equal(created.Installments[0].InterestPart, payment.Lines.Where(l => l.AccountId != loanAccount.Id && l.Debit > 0).Sum(l => l.Debit));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.PayInstallmentAsync(created.Installments[0].Id, new PayInstallmentDto()));   // already paid
    }

    [Fact]
    public async Task Create_RejectsBadInput()
    {
        var (ctx, bank, loanAccount) = await SeedAsync();
        var svc = new LoanService(ctx);
        CreateLoanDto Make(Action<CreateLoanDto> tweak)
        {
            var d = new CreateLoanDto { Description = "x", Principal = 1000, TermMonths = 6, DisbursementDate = new DateTime(2026, 11, 1), FirstInstallmentDate = new DateTime(2026, 12, 1), LoanAccountId = loanAccount.Id, BankAccountId = bank.Id };
            tweak(d);
            return d;
        }

        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(Make(d => d.Principal = 0)));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(Make(d => d.TermMonths = 0)));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(Make(d => d.AnnualInterestPercent = 150)));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(Make(d => d.FirstInstallmentDate = new DateTime(2026, 10, 1))));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(Make(d => d.LoanAccountId = bank.Id)));   // the loan must sit in a liability account
    }
}

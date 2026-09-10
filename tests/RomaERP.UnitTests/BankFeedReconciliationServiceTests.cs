using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Stub <see cref="IBankFeedProvider"/> for tests — never makes a real call, just returns
/// whatever the test configured, mirroring FakeExchangeRateProvider's role in MultiCurrencyTests.</summary>
public class FakeBankFeedProvider : IBankFeedProvider
{
    public string Name => "Fake";
    public bool IsConfigured { get; set; }
    public BankFeedFetchResult Result { get; set; } = new(true, new List<BankFeedTransactionData>(), null);

    public Task<BankFeedFetchResult> FetchTransactionsAsync(BankFeedFetchRequest request, CancellationToken ct = default)
        => Task.FromResult(Result);
}

public class BankFeedReconciliationServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account bank, Account revenue, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();

        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "January 2026", PeriodNumber = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) };

        ctx.Accounts.AddRange(bank, revenue);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        return (ctx, bank, revenue, period);
    }

    private static JournalEntry PostedDeposit(Guid bankAccountId, Guid revenueAccountId, Guid fiscalPeriodId, string entryNumber, DateTime date, decimal amount) => new()
    {
        EntryNumber = entryNumber,
        EntryDate = date,
        FiscalPeriodId = fiscalPeriodId,
        Status = JournalEntryStatus.Posted,
        Lines =
        {
            new JournalEntryLine { LineNumber = 1, AccountId = bankAccountId, Debit = amount, Credit = 0 },
            new JournalEntryLine { LineNumber = 2, AccountId = revenueAccountId, Debit = 0, Credit = amount }
        }
    };

    [Fact]
    public async Task ImportCsvAsync_ParsesLinesAndAutoMatchesAgainstAPostedGlLine()
    {
        var (ctx, bank, revenue, period) = await SeedAsync();
        ctx.JournalEntries.Add(PostedDeposit(bank.Id, revenue.Id, period.Id, "JV-000001", new DateTime(2026, 1, 10), 5000m));
        await ctx.SaveChangesAsync();

        var service = new BankFeedReconciliationService(ctx, new FakeBankFeedProvider());
        var csv = "Date,Description,Amount\n2026-01-10,Deposit,5000\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportCsvAsync(stream, bank.Id, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.AutoMatchedCount);
        var line = Assert.Single(await ctx.BankFeedTransactions.ToListAsync());
        Assert.True(line.IsMatched);
        Assert.Equal("Manual", line.Source);
    }

    [Fact]
    public async Task AutoMatchAsync_SkipsWhenMultipleCandidatesShareTheSameAmountAndDate()
    {
        var (ctx, bank, revenue, period) = await SeedAsync();
        ctx.JournalEntries.Add(PostedDeposit(bank.Id, revenue.Id, period.Id, "JV-000001", new DateTime(2026, 1, 10), 1000m));
        ctx.JournalEntries.Add(PostedDeposit(bank.Id, revenue.Id, period.Id, "JV-000002", new DateTime(2026, 1, 11), 1000m));
        ctx.BankFeedTransactions.Add(new BankFeedTransaction { AccountId = bank.Id, TransactionDate = new DateTime(2026, 1, 10), Description = "x", Amount = 1000m, Source = "Manual" });
        await ctx.SaveChangesAsync();

        var service = new BankFeedReconciliationService(ctx, new FakeBankFeedProvider());
        var matched = await service.AutoMatchAsync(bank.Id, CancellationToken.None);

        Assert.Equal(0, matched);
    }

    [Fact]
    public async Task MatchManualAsync_MatchesThenRejectsAConflictingSecondMatch()
    {
        var (ctx, bank, revenue, period) = await SeedAsync();
        var entry = PostedDeposit(bank.Id, revenue.Id, period.Id, "JV-000001", new DateTime(2026, 1, 10), 3000m);
        ctx.JournalEntries.Add(entry);
        var feedLine = new BankFeedTransaction { AccountId = bank.Id, TransactionDate = new DateTime(2026, 1, 10), Description = "x", Amount = 3000m, Source = "Manual" };
        var otherFeedLine = new BankFeedTransaction { AccountId = bank.Id, TransactionDate = new DateTime(2026, 1, 10), Description = "y", Amount = 3000m, Source = "Manual" };
        ctx.BankFeedTransactions.AddRange(feedLine, otherFeedLine);
        await ctx.SaveChangesAsync();

        var bankLineId = entry.Lines.First(l => l.AccountId == bank.Id).Id;
        var service = new BankFeedReconciliationService(ctx, new FakeBankFeedProvider());

        var matched = await service.MatchManualAsync(new ManualMatchBankFeedDto { BankFeedTransactionId = feedLine.Id, JournalEntryLineId = bankLineId });
        Assert.True(matched.IsMatched);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.MatchManualAsync(new ManualMatchBankFeedDto { BankFeedTransactionId = otherFeedLine.Id, JournalEntryLineId = bankLineId }));
    }

    [Fact]
    public async Task UnmatchAsync_ClearsTheMatch()
    {
        var (ctx, bank, revenue, period) = await SeedAsync();
        var entry = PostedDeposit(bank.Id, revenue.Id, period.Id, "JV-000001", new DateTime(2026, 1, 10), 2000m);
        ctx.JournalEntries.Add(entry);
        await ctx.SaveChangesAsync();
        var bankLineId = entry.Lines.First(l => l.AccountId == bank.Id).Id;

        var feedLine = new BankFeedTransaction { AccountId = bank.Id, TransactionDate = new DateTime(2026, 1, 10), Description = "x", Amount = 2000m, Source = "Manual" };
        ctx.BankFeedTransactions.Add(feedLine);
        await ctx.SaveChangesAsync();

        var service = new BankFeedReconciliationService(ctx, new FakeBankFeedProvider());
        await service.MatchManualAsync(new ManualMatchBankFeedDto { BankFeedTransactionId = feedLine.Id, JournalEntryLineId = bankLineId });

        var unmatched = await service.UnmatchAsync(feedLine.Id);

        Assert.False(unmatched.IsMatched);
        Assert.Null(unmatched.MatchedJournalEntryLineId);
    }

    [Fact]
    public async Task SyncLiveAsync_FailsGracefullyWhenProviderIsNotConfigured()
    {
        var (ctx, bank, _, _) = await SeedAsync();
        var service = new BankFeedReconciliationService(ctx, new FakeBankFeedProvider { IsConfigured = false });

        var result = await service.SyncLiveAsync(bank.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Empty(await ctx.BankFeedTransactions.ToListAsync());
    }

    [Fact]
    public async Task SyncLiveAsync_ImportsOnceAndSkipsAlreadySyncedExternalIdsOnAReRun()
    {
        var (ctx, bank, _, _) = await SeedAsync();
        var provider = new FakeBankFeedProvider
        {
            IsConfigured = true,
            Result = new BankFeedFetchResult(true, new List<BankFeedTransactionData>
            {
                new("EXT-1", new DateTime(2026, 1, 5), "Live deposit", 1500m)
            }, null)
        };
        var service = new BankFeedReconciliationService(ctx, provider);

        var first = await service.SyncLiveAsync(bank.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
        var second = await service.SyncLiveAsync(bank.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(1, first.ImportedCount);
        Assert.Equal(0, second.ImportedCount);
        Assert.Single(await ctx.BankFeedTransactions.ToListAsync());
    }

    [Fact]
    public async Task GetSummaryAsync_ReportsUnmatchedLinesOnBothSides()
    {
        var (ctx, bank, revenue, period) = await SeedAsync();
        ctx.JournalEntries.Add(PostedDeposit(bank.Id, revenue.Id, period.Id, "JV-000001", new DateTime(2026, 1, 10), 4000m));
        ctx.BankFeedTransactions.Add(new BankFeedTransaction { AccountId = bank.Id, TransactionDate = new DateTime(2026, 1, 12), Description = "unmatched deposit", Amount = 900m, Source = "Manual" });
        await ctx.SaveChangesAsync();

        var service = new BankFeedReconciliationService(ctx, new FakeBankFeedProvider());
        var summary = await service.GetSummaryAsync(bank.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(0, summary.MatchedCount);
        Assert.Single(summary.UnmatchedFeedLines);
        Assert.Single(summary.UnmatchedGlLines);
        Assert.Equal(900m, summary.FeedNetMovement);
        Assert.Equal(4000m, summary.GlNetMovement);
        Assert.False(summary.IsBalanced);
    }
}

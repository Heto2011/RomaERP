using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

/// <summary>Reconciles a bank GL account's posted journal-entry lines against its real-world activity —
/// brought in by CSV upload today (<see cref="ImportCsvAsync"/>) and, once a live <see cref="IBankFeedProvider"/>
/// is configured, automatically (<see cref="SyncLiveAsync"/>). Matching is amount + date-window, the same
/// approach already used by the AI assistant's card-expense reconciliation (<c>BankReconciliationService</c>),
/// just against GL postings instead of expense captures.</summary>
public class BankFeedReconciliationService : IBankFeedReconciliationService
{
    private static readonly TimeSpan MatchWindow = TimeSpan.FromDays(5);

    private readonly IApplicationDbContext _context;
    private readonly IBankFeedProvider _provider;

    public BankFeedReconciliationService(IApplicationDbContext context, IBankFeedProvider provider)
    {
        _context = context;
        _provider = provider;
    }

    public BankFeedProviderStatusDto GetProviderStatus() => new()
    {
        Name = _provider.Name,
        IsConfigured = _provider.IsConfigured
    };

    public async Task<ImportBankFeedCsvResultDto> ImportCsvAsync(Stream csvStream, Guid accountId, CancellationToken ct = default)
    {
        await GetAccountAsync(accountId, ct);

        var lines = await ParseCsvAsync(csvStream, accountId, ct);
        if (lines.Count == 0)
            throw new ValidationAppException("لم يتم العثور على أي حركات في الملف المرفوع. تأكد من صيغة الملف: Date,Description,Amount (موجب = إيداع، سالب = سحب).");

        _context.BankFeedTransactions.AddRange(lines);
        await _context.SaveChangesAsync(ct);

        var matched = await AutoMatchAsync(accountId, ct);

        return new ImportBankFeedCsvResultDto { ImportedCount = lines.Count, AutoMatchedCount = matched };
    }

    public async Task<SyncLiveBankFeedResultDto> SyncLiveAsync(Guid accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        if (!_provider.IsConfigured)
            return new SyncLiveBankFeedResultDto
            {
                Success = false,
                FailureReason = "التغذية البنكية اللحظية غير مفعّلة بعد — لسه مفيش مزود Open Banking متصل. استخدم رفع كشف الحساب (CSV) لحد ما يتم توصيل مزود."
            };

        await GetAccountAsync(accountId, ct);

        var result = await _provider.FetchTransactionsAsync(new BankFeedFetchRequest(accountId, fromDate, toDate), ct);
        if (!result.Success)
            return new SyncLiveBankFeedResultDto { Success = false, FailureReason = result.FailureReason };

        var existingExternalIds = (await _context.BankFeedTransactions
                .Where(t => t.AccountId == accountId && t.ExternalId != null)
                .Select(t => t.ExternalId!)
                .ToListAsync(ct))
            .ToHashSet();

        var newTransactions = result.Transactions
            .Where(t => !existingExternalIds.Contains(t.ExternalId))
            .Select(t => new BankFeedTransaction
            {
                AccountId = accountId,
                TransactionDate = t.TransactionDate,
                Description = t.Description,
                Amount = t.Amount,
                Source = "Live",
                ExternalId = t.ExternalId
            })
            .ToList();

        if (newTransactions.Count > 0)
        {
            _context.BankFeedTransactions.AddRange(newTransactions);
            await _context.SaveChangesAsync(ct);
        }

        var matched = await AutoMatchAsync(accountId, ct);

        return new SyncLiveBankFeedResultDto { Success = true, ImportedCount = newTransactions.Count, AutoMatchedCount = matched };
    }

    public async Task<BankFeedReconciliationSummaryDto> GetSummaryAsync(Guid accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountId, ct);

        var feedLines = await _context.BankFeedTransactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId && t.TransactionDate >= fromDate && t.TransactionDate <= toDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(ct);

        var glLines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == accountId
                        && l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fromDate
                        && l.JournalEntry.EntryDate <= toDate)
            .ToListAsync(ct);

        var matchedGlLineIds = feedLines
            .Where(f => f.MatchedJournalEntryLineId != null)
            .Select(f => f.MatchedJournalEntryLineId!.Value)
            .ToHashSet();

        var unmatchedGlLines = glLines
            .Where(l => !matchedGlLineIds.Contains(l.Id))
            .Select(l => new UnmatchedGlLineDto
            {
                JournalEntryLineId = l.Id,
                JournalEntryId = l.JournalEntryId,
                EntryNumber = l.JournalEntry!.EntryNumber,
                EntryDate = l.JournalEntry.EntryDate,
                Description = l.Description,
                Amount = l.Debit - l.Credit
            })
            .OrderByDescending(l => l.EntryDate)
            .ToList();

        return new BankFeedReconciliationSummaryDto
        {
            AccountId = account.Id,
            AccountName = account.NameAr,
            FromDate = fromDate,
            ToDate = toDate,
            MatchedCount = feedLines.Count(f => f.IsMatched),
            UnmatchedFeedLines = feedLines.Where(f => !f.IsMatched).Select(Map).ToList(),
            UnmatchedGlLines = unmatchedGlLines,
            FeedNetMovement = feedLines.Sum(f => f.Amount),
            GlNetMovement = glLines.Sum(l => l.Debit - l.Credit)
        };
    }

    public async Task<int> AutoMatchAsync(Guid accountId, CancellationToken ct = default)
    {
        var unmatchedFeed = await _context.BankFeedTransactions
            .Where(t => t.AccountId == accountId && !t.IsMatched)
            .ToListAsync(ct);

        if (unmatchedFeed.Count == 0)
            return 0;

        var matchedLineIds = (await _context.BankFeedTransactions
                .Where(t => t.AccountId == accountId && t.MatchedJournalEntryLineId != null)
                .Select(t => t.MatchedJournalEntryLineId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        var glLines = await _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == accountId
                        && l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted)
            .ToListAsync(ct);

        var matchedCount = 0;

        foreach (var feedLine in unmatchedFeed)
        {
            var candidates = glLines
                .Where(l => !matchedLineIds.Contains(l.Id)
                            && (l.Debit - l.Credit) == feedLine.Amount
                            && (l.JournalEntry!.EntryDate - feedLine.TransactionDate).Duration() <= MatchWindow)
                .ToList();

            if (candidates.Count != 1)
                continue;

            feedLine.IsMatched = true;
            feedLine.MatchedJournalEntryLineId = candidates[0].Id;
            matchedLineIds.Add(candidates[0].Id);
            matchedCount++;
        }

        if (matchedCount > 0)
            await _context.SaveChangesAsync(ct);

        return matchedCount;
    }

    public async Task<BankFeedTransactionDto> MatchManualAsync(ManualMatchBankFeedDto dto, CancellationToken ct = default)
    {
        var feedLine = await _context.BankFeedTransactions.FirstOrDefaultAsync(t => t.Id == dto.BankFeedTransactionId, ct)
            ?? throw new NotFoundException(nameof(BankFeedTransaction), dto.BankFeedTransactionId);

        if (feedLine.IsMatched)
            throw new ValidationAppException("حركة التغذية البنكية هذه متطابقة بالفعل.");

        var glLine = await _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .FirstOrDefaultAsync(l => l.Id == dto.JournalEntryLineId, ct)
            ?? throw new NotFoundException(nameof(JournalEntryLine), dto.JournalEntryLineId);

        if (glLine.AccountId != feedLine.AccountId)
            throw new ValidationAppException("لا يمكن مطابقة حركة على حساب بنكي مختلف.");

        var alreadyMatched = await _context.BankFeedTransactions
            .AnyAsync(t => t.MatchedJournalEntryLineId == dto.JournalEntryLineId, ct);
        if (alreadyMatched)
            throw new ValidationAppException("هذا القيد المحاسبي متطابق بالفعل مع حركة أخرى.");

        feedLine.IsMatched = true;
        feedLine.MatchedJournalEntryLineId = glLine.Id;
        await _context.SaveChangesAsync(ct);

        return Map(feedLine);
    }

    public async Task<BankFeedTransactionDto> UnmatchAsync(Guid bankFeedTransactionId, CancellationToken ct = default)
    {
        var feedLine = await _context.BankFeedTransactions.FirstOrDefaultAsync(t => t.Id == bankFeedTransactionId, ct)
            ?? throw new NotFoundException(nameof(BankFeedTransaction), bankFeedTransactionId);

        feedLine.IsMatched = false;
        feedLine.MatchedJournalEntryLineId = null;
        await _context.SaveChangesAsync(ct);

        return Map(feedLine);
    }

    private async Task<Account> GetAccountAsync(Guid accountId, CancellationToken ct)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct)
           ?? throw new NotFoundException(nameof(Account), accountId);

    private static async Task<List<BankFeedTransaction>> ParseCsvAsync(Stream csvStream, Guid accountId, CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream);
        var lines = new List<BankFeedTransaction>();
        var isFirstLine = true;

        while (await reader.ReadLineAsync(ct) is { } rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var fields = rawLine.Split(',').Select(f => f.Trim().Trim('"')).ToArray();

            if (isFirstLine)
            {
                isFirstLine = false;
                if (fields.Length > 0 && !decimal.TryParse(fields.Last(), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    continue; // header row
            }

            if (fields.Length < 3)
                continue;

            if (!DateTime.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            if (!decimal.TryParse(fields[^1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                continue;

            var description = string.Join(",", fields.Skip(1).Take(fields.Length - 2));

            lines.Add(new BankFeedTransaction
            {
                AccountId = accountId,
                TransactionDate = date,
                Description = description,
                Amount = amount,
                Source = "Manual"
            });
        }

        return lines;
    }

    private static BankFeedTransactionDto Map(BankFeedTransaction t) => new()
    {
        Id = t.Id,
        AccountId = t.AccountId,
        TransactionDate = t.TransactionDate,
        Description = t.Description,
        Amount = t.Amount,
        Source = t.Source,
        IsMatched = t.IsMatched,
        MatchedJournalEntryLineId = t.MatchedJournalEntryLineId
    };
}

using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Billing;

public record MarketerCurrencyTotals(string Currency, decimal Waiting, decimal Payable, decimal PaidOut);
public record MarketerSummaryDto(Guid Id, string Name, string Code, string? Email, string? Notes, bool IsActive,
    int Referrals, int PayingCustomers, List<MarketerCurrencyTotals> Totals);
public record MarketerCommissionDto(Guid Id, string CompanyCode, string CompanyName, DateTime PaymentPaidAtUtc, decimal PaymentAmount,
    string Currency, decimal Rate, decimal Amount, DateTime EligibleAtUtc, CommissionStatus Status, DateTime? PaidOutAtUtc, string? Note);

/// <summary>Referral codes and the commission each referred company's payments earn. Commissions are derived from the paid
/// subscription invoices (card payments and manually confirmed ones alike) by <see cref="SyncAsync"/>, which is idempotent:
/// one commission row per paid invoice, so nothing is counted twice and no payment code path needs to know about marketers.</summary>
public class MarketerCommissionService
{
    private readonly CentralDbContext _central;

    public MarketerCommissionService(CentralDbContext central) => _central = central;

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I, easy to read out loud

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = new string(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());
            if (!await _central.Marketers.AnyAsync(m => m.Code == code, ct)) return code;
        }
        throw new InvalidOperationException("Could not generate a unique marketer code.");
    }

    /// <summary>Creates a marketer. The code is generated automatically (6 easy-to-read characters) unless one is given.</summary>
    public async Task<MarketerSummaryDto> CreateAsync(string name, string? code, string? email, string? notes, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(code) ? await GenerateCodeAsync(ct) : NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationAppException("اسم المسوّق مطلوب.");
        if (normalized.Length < 3 || normalized.Length > 40 || normalized.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ValidationAppException("الكود لازم يكون من 3 لـ 40 حرف إنجليزي أو رقم (ممكن - أو _).");
        if (await _central.Marketers.AnyAsync(m => m.Code == normalized, ct))
            throw new ValidationAppException("الكود ده مستخدم قبل كده.");

        var marketer = new Marketer { Name = name.Trim(), Code = normalized, Email = email?.Trim(), Notes = notes?.Trim() };
        _central.Marketers.Add(marketer);
        await _central.SaveChangesAsync(ct);
        return new MarketerSummaryDto(marketer.Id, marketer.Name, marketer.Code, marketer.Email, marketer.Notes, true, 0, 0, new());
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var marketer = await _central.Marketers.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(Marketer), id);
        marketer.IsActive = active;
        await _central.SaveChangesAsync(ct);
    }

    /// <summary>Links a freshly created company to the marketer whose code it signed up with. An unknown or inactive code is
    /// ignored on purpose: a typo must never stop someone from creating their account.</summary>
    public async Task RecordReferralAsync(Guid tenantId, string? code, CancellationToken ct = default)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length == 0) return;
        var marketer = await _central.Marketers.AsNoTracking().FirstOrDefaultAsync(m => m.Code == normalized && m.IsActive, ct);
        if (marketer is null) return;
        if (await _central.MarketerReferrals.AnyAsync(r => r.TenantId == tenantId, ct)) return;
        _central.MarketerReferrals.Add(new MarketerReferral { TenantId = tenantId, MarketerId = marketer.Id });
        await _central.SaveChangesAsync(ct);
    }

    /// <summary>Creates the commission row for every paid invoice of a referred company that does not have one yet.</summary>
    public async Task SyncAsync(CancellationToken ct = default)
    {
        var referrals = await _central.MarketerReferrals.AsNoTracking().ToListAsync(ct);
        if (referrals.Count == 0) return;
        var tenantIds = referrals.Select(r => r.TenantId).ToList();

        var paid = await _central.SubscriptionInvoices.AsNoTracking()
            .Where(i => tenantIds.Contains(i.TenantId) && i.Status == SubscriptionInvoiceStatus.Paid && i.PaidAtUtc != null && i.TotalAmount > 0)
            .ToListAsync(ct);
        if (paid.Count == 0) return;

        var done = (await _central.MarketerCommissions.AsNoTracking().Select(c => c.InvoiceId).ToListAsync(ct)).ToHashSet();
        var marketerByTenant = referrals.ToDictionary(r => r.TenantId, r => r.MarketerId);

        foreach (var group in paid.GroupBy(i => i.TenantId))
        {
            var firstPayment = group.Min(i => i.PaidAtUtc!.Value);
            foreach (var invoice in group.Where(i => !done.Contains(i.Id)))
            {
                var paidAt = invoice.PaidAtUtc!.Value;
                var rate = MarketerCommissionPolicy.RateFor(firstPayment, paidAt);
                if (rate <= 0) continue;
                _central.MarketerCommissions.Add(new MarketerCommission
                {
                    MarketerId = marketerByTenant[group.Key],
                    TenantId = group.Key,
                    InvoiceId = invoice.Id,
                    PaymentPaidAtUtc = paidAt,
                    PaymentAmount = invoice.TotalAmount,
                    Currency = invoice.Currency,
                    Rate = rate,
                    Amount = Math.Round(invoice.TotalAmount * rate, 2, MidpointRounding.AwayFromZero),
                    EligibleAtUtc = paidAt.AddDays(MarketerCommissionPolicy.HoldDays),
                });
            }
        }
        await _central.SaveChangesAsync(ct);
    }

    public async Task<List<MarketerSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        await SyncAsync(ct);
        var now = DateTime.UtcNow;
        var marketers = await _central.Marketers.AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct);
        var referrals = await _central.MarketerReferrals.AsNoTracking().GroupBy(r => r.MarketerId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var commissions = await _central.MarketerCommissions.AsNoTracking().Where(c => c.Status != CommissionStatus.Voided).ToListAsync(ct);

        return marketers.Select(m =>
        {
            var mine = commissions.Where(c => c.MarketerId == m.Id).ToList();
            var totals = mine.GroupBy(c => c.Currency).Select(g => new MarketerCurrencyTotals(
                g.Key,
                g.Where(c => c.Status == CommissionStatus.Accrued && c.EligibleAtUtc > now).Sum(c => c.Amount),
                g.Where(c => c.Status == CommissionStatus.Accrued && c.EligibleAtUtc <= now).Sum(c => c.Amount),
                g.Where(c => c.Status == CommissionStatus.PaidOut).Sum(c => c.Amount))).ToList();
            return new MarketerSummaryDto(m.Id, m.Name, m.Code, m.Email, m.Notes, m.IsActive,
                referrals.FirstOrDefault(r => r.Key == m.Id)?.Count ?? 0, mine.Select(c => c.TenantId).Distinct().Count(), totals);
        }).ToList();
    }

    public async Task<List<MarketerCommissionDto>> GetCommissionsAsync(Guid marketerId, CancellationToken ct = default)
    {
        await SyncAsync(ct);
        var rows = await _central.MarketerCommissions.AsNoTracking().Where(c => c.MarketerId == marketerId)
            .OrderByDescending(c => c.PaymentPaidAtUtc).ToListAsync(ct);
        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
        var tenants = await _central.Tenants.AsNoTracking().Where(t => tenantIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        return rows.Select(c => new MarketerCommissionDto(c.Id,
            tenants.TryGetValue(c.TenantId, out var t) ? t.CompanyCode : "—", t?.CompanyNameEn ?? "—",
            c.PaymentPaidAtUtc, c.PaymentAmount, c.Currency, c.Rate, c.Amount, c.EligibleAtUtc, c.Status, c.PaidOutAtUtc, c.Note)).ToList();
    }

    /// <summary>Marks every payable commission of a marketer (past its hold period) as paid out, and returns the total per currency.</summary>
    public async Task<List<MarketerCurrencyTotals>> PayOutAsync(Guid marketerId, string? note, CancellationToken ct = default)
    {
        await SyncAsync(ct);
        var now = DateTime.UtcNow;
        var due = await _central.MarketerCommissions
            .Where(c => c.MarketerId == marketerId && c.Status == CommissionStatus.Accrued && c.EligibleAtUtc <= now).ToListAsync(ct);
        foreach (var c in due)
        {
            c.Status = CommissionStatus.PaidOut;
            c.PaidOutAtUtc = now;
            if (!string.IsNullOrWhiteSpace(note)) c.Note = note.Trim();
        }
        await _central.SaveChangesAsync(ct);
        return due.GroupBy(c => c.Currency).Select(g => new MarketerCurrencyTotals(g.Key, 0, 0, g.Sum(c => c.Amount))).ToList();
    }

    public async Task VoidAsync(Guid commissionId, string? reason, CancellationToken ct = default)
    {
        var c = await _central.MarketerCommissions.FirstOrDefaultAsync(x => x.Id == commissionId, ct) ?? throw new NotFoundException(nameof(MarketerCommission), commissionId);
        if (c.Status == CommissionStatus.PaidOut) throw new ValidationAppException("العمولة دي اتصرفت قبل كده، مش ممكن تتلغي.");
        c.Status = CommissionStatus.Voided;
        c.Note = string.IsNullOrWhiteSpace(reason) ? "Voided" : reason.Trim();
        await _central.SaveChangesAsync(ct);
    }
}

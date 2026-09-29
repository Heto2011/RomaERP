using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly IApplicationDbContext _context;
    private readonly IExchangeRateProvider _provider;

    public ExchangeRateService(IApplicationDbContext context, IExchangeRateProvider provider)
    {
        _context = context;
        _provider = provider;
    }

    public async Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct = default)
    {
        var rates = await _context.ExchangeRates
            .AsNoTracking()
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.CurrencyCode)
            .ToListAsync(ct);

        return rates.Select(Map).ToList();
    }

    public async Task<ExchangeRateDto> SetRateAsync(SetExchangeRateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrencyCode))
            throw new ValidationAppException("كود العملة مطلوب.");
        if (dto.RateToFunctional <= 0)
            throw new ValidationAppException("سعر الصرف يجب أن يكون أكبر من صفر.");

        var currencyCode = dto.CurrencyCode.Trim().ToUpperInvariant();
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (currencyCode == (settings?.DefaultCurrency ?? "EGP"))
            throw new ValidationAppException("العملة الأساسية للشركة مش محتاجة سعر صرف — سعرها دايمًا 1.");

        var rateDate = dto.RateDate.Date;

        var existing = await _context.ExchangeRates
            .FirstOrDefaultAsync(r => r.CurrencyCode == currencyCode && r.RateDate == rateDate, ct);

        if (existing is not null)
        {
            existing.RateToFunctional = dto.RateToFunctional;
            existing.Source = "Manual";
        }
        else
        {
            existing = new ExchangeRate
            {
                CurrencyCode = currencyCode,
                RateDate = rateDate,
                RateToFunctional = dto.RateToFunctional,
                Source = "Manual"
            };
            _context.ExchangeRates.Add(existing);
        }

        await _context.SaveChangesAsync(ct);

        return Map(existing);
    }

    public async Task<ExchangeRateDto> AddTrackedCurrencyAsync(string currencyCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ValidationAppException("كود العملة مطلوب.");

        var code = currencyCode.Trim().ToUpperInvariant();
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var functionalCurrency = settings?.DefaultCurrency ?? "EGP";
        if (code == functionalCurrency)
            throw new ValidationAppException("العملة الأساسية للشركة مش محتاجة سعر صرف — سعرها دايمًا 1.");

        var liveRate = await _provider.GetRateAsync(code, functionalCurrency, ct)
            ?? throw new ValidationAppException($"معرفناش نجيب سعر صرف حي لعملة {code} دلوقتي. تأكد إن كود العملة صح (زي USD أو EUR) أو حاول تاني بعد شوية.");

        var today = DateTime.UtcNow.Date;
        var existing = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.CurrencyCode == code && r.RateDate == today, ct);
        if (existing is not null)
        {
            existing.RateToFunctional = liveRate;
            existing.Source = "Auto";
        }
        else
        {
            existing = new ExchangeRate { CurrencyCode = code, RateDate = today, RateToFunctional = liveRate, Source = "Auto" };
            _context.ExchangeRates.Add(existing);
        }

        await _context.SaveChangesAsync(ct);

        return Map(existing);
    }

    public async Task RefreshAllTrackedCurrenciesAsync(CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var functionalCurrency = settings?.DefaultCurrency ?? "EGP";

        var trackedCurrencies = await _context.ExchangeRates
            .AsNoTracking()
            .Select(r => r.CurrencyCode)
            .Distinct()
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        foreach (var code in trackedCurrencies)
        {
            var liveRate = await _provider.GetRateAsync(code, functionalCurrency, ct);
            if (liveRate is null)
                continue;

            var existing = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.CurrencyCode == code && r.RateDate == today, ct);
            if (existing is not null)
            {
                // Never clobber a user's manual override for today.
                if (existing.Source == "Manual")
                    continue;
                existing.RateToFunctional = liveRate.Value;
            }
            else
            {
                _context.ExchangeRates.Add(new ExchangeRate { CurrencyCode = code, RateDate = today, RateToFunctional = liveRate.Value, Source = "Auto" });
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private static ExchangeRateDto Map(ExchangeRate r) => new()
    {
        Id = r.Id,
        CurrencyCode = r.CurrencyCode,
        RateDate = r.RateDate,
        RateToFunctional = r.RateToFunctional,
        Source = r.Source
    };

    public async Task<(string CurrencyCode, decimal RateToFunctional)> ResolveAsync(string? requestedCurrencyCode, DateTime date, CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var functionalCurrency = settings?.DefaultCurrency ?? "EGP";

        var currencyCode = string.IsNullOrWhiteSpace(requestedCurrencyCode)
            ? functionalCurrency
            : requestedCurrencyCode.Trim().ToUpperInvariant();

        if (currencyCode == functionalCurrency)
            return (functionalCurrency, 1m);

        var rate = await _context.ExchangeRates
            .AsNoTracking()
            .Where(r => r.CurrencyCode == currencyCode && r.RateDate <= date.Date)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefaultAsync(ct);

        if (rate is null)
            throw new ValidationAppException($"لا يوجد سعر صرف مسجل لعملة {currencyCode} في تاريخ {date:yyyy-MM-dd} أو قبله. أضف سعر الصرف أولاً من شاشة أسعار الصرف.");

        return (currencyCode, rate.RateToFunctional);
    }
}

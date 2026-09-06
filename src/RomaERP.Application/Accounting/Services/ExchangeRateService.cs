using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly IApplicationDbContext _context;

    public ExchangeRateService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct = default)
    {
        return await _context.ExchangeRates
            .AsNoTracking()
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.CurrencyCode)
            .Select(r => new ExchangeRateDto
            {
                Id = r.Id,
                CurrencyCode = r.CurrencyCode,
                RateDate = r.RateDate,
                RateToFunctional = r.RateToFunctional
            })
            .ToListAsync(ct);
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
        }
        else
        {
            existing = new ExchangeRate
            {
                CurrencyCode = currencyCode,
                RateDate = rateDate,
                RateToFunctional = dto.RateToFunctional
            };
            _context.ExchangeRates.Add(existing);
        }

        await _context.SaveChangesAsync(ct);

        return new ExchangeRateDto
        {
            Id = existing.Id,
            CurrencyCode = existing.CurrencyCode,
            RateDate = existing.RateDate,
            RateToFunctional = existing.RateToFunctional
        };
    }

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

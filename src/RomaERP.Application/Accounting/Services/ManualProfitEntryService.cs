using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class ManualProfitEntryService : IManualProfitEntryService
{
    private readonly IApplicationDbContext _context;

    public ManualProfitEntryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ManualProfitEntryDto>> GetAllAsync(ManualProfitDimension dimension, CancellationToken ct = default)
    {
        return await _context.ManualProfitEntries
            .AsNoTracking()
            .Where(e => e.Dimension == dimension)
            .OrderByDescending(e => e.PeriodMonth)
            .ThenBy(e => e.Name)
            .Select(e => Map(e))
            .ToListAsync(ct);
    }

    public async Task<ManualProfitEntryDto> CreateAsync(CreateManualProfitEntryDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationAppException("الاسم مطلوب.");

        var entry = new ManualProfitEntry
        {
            Dimension = (ManualProfitDimension)dto.Dimension,
            Name = dto.Name.Trim(),
            PeriodMonth = dto.PeriodMonth,
            Revenue = dto.Revenue,
            Cost = dto.Cost
        };

        _context.ManualProfitEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        return Map(entry);
    }

    public async Task<ManualProfitEntryDto> UpdateAsync(Guid id, UpdateManualProfitEntryDto dto, CancellationToken ct = default)
    {
        var entry = await _context.ManualProfitEntries.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(ManualProfitEntry), id);

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationAppException("الاسم مطلوب.");

        entry.Name = dto.Name.Trim();
        entry.PeriodMonth = dto.PeriodMonth;
        entry.Revenue = dto.Revenue;
        entry.Cost = dto.Cost;

        await _context.SaveChangesAsync(ct);
        return Map(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.ManualProfitEntries.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(ManualProfitEntry), id);

        entry.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static ManualProfitEntryDto Map(ManualProfitEntry e)
    {
        var grossProfit = e.Revenue - e.Cost;
        return new ManualProfitEntryDto
        {
            Id = e.Id,
            Dimension = (int)e.Dimension,
            Name = e.Name,
            PeriodMonth = e.PeriodMonth,
            Revenue = e.Revenue,
            Cost = e.Cost,
            GrossProfit = grossProfit,
            MarginPercent = e.Revenue != 0 ? Math.Round(grossProfit / e.Revenue * 100, 2) : 0
        };
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

/// <summary>Imports a delivery-platform settlement report (CSV: Date,Description,Amount) and compares the
/// total received against real Delivery-channel revenue already recorded in RomaERP for the same period.
/// Works the same way as bank reconciliation — a file the user exports from the platform's own dashboard, no
/// live API integration (none of the delivery platforms offer merchant API access here).</summary>
public class DeliveryReconciliationService : IDeliveryReconciliationService
{
    private readonly IApplicationDbContext _context;

    public DeliveryReconciliationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeliverySettlementImportDto> ImportAsync(Stream csvStream, string fileName, string platformName, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            throw new ValidationAppException("اسم منصة التوصيل مطلوب.");

        var lines = await ParseCsvAsync(csvStream, ct);
        if (lines.Count == 0)
            throw new ValidationAppException("لم يتم العثور على أي حركات في الملف المرفوع. تأكد من صيغة الملف: Date,Description,Amount.");

        var import = new DeliverySettlementImport
        {
            FileName = fileName,
            PlatformName = platformName.Trim(),
            PeriodFrom = lines.Min(l => l.TransactionDate),
            PeriodTo = lines.Max(l => l.TransactionDate),
            ImportedByUserId = userId,
            Lines = lines
        };

        _context.DeliverySettlementImports.Add(import);
        await _context.SaveChangesAsync(ct);

        return Map(import);
    }

    public async Task<List<DeliverySettlementImportDto>> GetImportsAsync(CancellationToken ct = default)
    {
        var imports = await _context.DeliverySettlementImports
            .AsNoTracking()
            .Include(i => i.Lines)
            .OrderByDescending(i => i.PeriodTo)
            .ToListAsync(ct);

        return imports.Select(Map).ToList();
    }

    public async Task<DeliveryReconciliationReportDto> GetReconciliationAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var expectedRevenue = await _context.RestaurantOrderLines
            .AsNoTracking()
            .Where(l => l.RestaurantOrder!.OrderType == RestaurantOrderType.Delivery
                        && l.RestaurantOrder.Status == RestaurantOrderStatus.Billed
                        && l.RestaurantOrder.OrderDate >= fromDate
                        && l.RestaurantOrder.OrderDate <= toDate)
            .SumAsync(l => l.LineTotal, ct);

        var receivedAmount = await _context.DeliverySettlementLines
            .AsNoTracking()
            .Where(l => l.TransactionDate >= fromDate && l.TransactionDate <= toDate)
            .SumAsync(l => l.Amount, ct);

        return new DeliveryReconciliationReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            ExpectedRevenue = expectedRevenue,
            ReceivedAmount = receivedAmount,
            Variance = receivedAmount - expectedRevenue
        };
    }

    private static async Task<List<DeliverySettlementLine>> ParseCsvAsync(Stream csvStream, CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream);
        var lines = new List<DeliverySettlementLine>();
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

            lines.Add(new DeliverySettlementLine
            {
                TransactionDate = date,
                Description = description,
                Amount = amount
            });
        }

        return lines;
    }

    private static DeliverySettlementImportDto Map(DeliverySettlementImport i) => new()
    {
        Id = i.Id,
        FileName = i.FileName,
        PlatformName = i.PlatformName,
        PeriodFrom = i.PeriodFrom,
        PeriodTo = i.PeriodTo,
        TotalAmount = i.Lines.Sum(l => l.Amount),
        LineCount = i.Lines.Count
    };
}

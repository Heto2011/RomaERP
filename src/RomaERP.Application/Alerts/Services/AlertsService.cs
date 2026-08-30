using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Alerts.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.Services;

namespace RomaERP.Application.Alerts.Services;

public class AlertsService : IAlertsService
{
    private const decimal HighWastePercentOfCogsThreshold = 5m;
    private const decimal LowMarginPercentThreshold = 10m;
    private const int MovementWindowDays = 30;

    private readonly IInventoryReportService _inventoryReportService;
    private readonly IFinancialReportService _financialReportService;
    private readonly ISalesService _salesService;
    private readonly IPurchasingService _purchasingService;

    public AlertsService(
        IInventoryReportService inventoryReportService,
        IFinancialReportService financialReportService,
        ISalesService salesService,
        IPurchasingService purchasingService)
    {
        _inventoryReportService = inventoryReportService;
        _financialReportService = financialReportService;
        _salesService = salesService;
        _purchasingService = purchasingService;
    }

    public async Task<AlertsReportDto> GetAlertsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-MovementWindowDays);
        var alerts = new List<AlertDto>();

        var movement = await _inventoryReportService.GetInventoryMovementAsync(windowStart, now, ct);
        var atRiskItems = movement.Items.Where(i => i.IsAtRiskOfStockout).ToList();
        if (atRiskItems.Count > 0)
        {
            alerts.Add(new AlertDto
            {
                Category = "Inventory",
                Severity = AlertSeverity.Warning,
                Title = $"{atRiskItems.Count} item(s) at risk of stockout",
                Detail = string.Join(", ", atRiskItems.Take(5).Select(i => $"{i.ItemCode} ({i.QuantityOnHand:0.##} left)"))
            });
        }

        var deadStockItems = movement.Items.Where(i => i.IsDeadStock).ToList();
        if (deadStockItems.Count > 0)
        {
            alerts.Add(new AlertDto
            {
                Category = "Inventory",
                Severity = AlertSeverity.Info,
                Title = $"{deadStockItems.Count} item(s) with no movement in {MovementWindowDays} days",
                Detail = string.Join(", ", deadStockItems.Take(5).Select(i => i.ItemCode))
            });
        }

        var cashFlow = await _financialReportService.GetCashFlowIntelligenceAsync(now, ct);
        if (cashFlow.FirstWeekBelowZero.HasValue)
        {
            alerts.Add(new AlertDto
            {
                Category = "Cash Flow",
                Severity = AlertSeverity.Critical,
                Title = "Projected cash balance goes negative",
                Detail = $"Based on the trailing average, the projected cash balance turns negative the week of {cashFlow.FirstWeekBelowZero.Value:yyyy-MM-dd}."
            });
        }

        var waste = await _inventoryReportService.GetWasteAnalysisAsync(windowStart, now, ct);
        if (waste.WasteCostPercentOfCogs is { } wastePct && wastePct > HighWastePercentOfCogsThreshold)
        {
            alerts.Add(new AlertDto
            {
                Category = "Waste",
                Severity = AlertSeverity.Warning,
                Title = $"Waste cost is {wastePct:0.#}% of COGS issued (last {MovementWindowDays} days)",
                Detail = $"Total waste cost {waste.TotalWasteCost:0.##} against {waste.CogsInPeriod:0.##} COGS issued."
            });
        }

        var itemProfitability = await _financialReportService.GetItemProfitabilityAsync(windowStart, now, ct);
        var lowMarginItems = itemProfitability.Items
            .Where(i => i.QuantitySold > 0 && i.MarginPercent < LowMarginPercentThreshold)
            .OrderBy(i => i.MarginPercent)
            .ToList();
        if (lowMarginItems.Count > 0)
        {
            alerts.Add(new AlertDto
            {
                Category = "Pricing",
                Severity = AlertSeverity.Warning,
                Title = $"{lowMarginItems.Count} sold item(s) below {LowMarginPercentThreshold:0}% margin",
                Detail = string.Join(", ", lowMarginItems.Take(5).Select(i => $"{i.ItemCode} ({i.MarginPercent:0.#}%)"))
            });
        }

        var arAging = await _salesService.GetArAgingAsync(now, ct);
        var overdueAr = arAging.Where(a => a.Days61To90 + a.Over90Days > 0).ToList();
        if (overdueAr.Count > 0)
        {
            alerts.Add(new AlertDto
            {
                Category = "Receivables",
                Severity = AlertSeverity.Warning,
                Title = $"{overdueAr.Count} customer(s) overdue 61+ days",
                Detail = string.Join(", ", overdueAr.Take(5).Select(a => $"{a.CustomerCode} ({(a.Days61To90 + a.Over90Days):0.##})"))
            });
        }

        var apAging = await _purchasingService.GetApAgingAsync(now, ct);
        var overdueAp = apAging.Where(a => a.Days61To90 + a.Over90Days > 0).ToList();
        if (overdueAp.Count > 0)
        {
            alerts.Add(new AlertDto
            {
                Category = "Payables",
                Severity = AlertSeverity.Info,
                Title = $"{overdueAp.Count} vendor(s) overdue 61+ days",
                Detail = string.Join(", ", overdueAp.Take(5).Select(a => $"{a.VendorCode} ({(a.Days61To90 + a.Over90Days):0.##})"))
            });
        }

        return new AlertsReportDto
        {
            GeneratedAt = now,
            Alerts = alerts.OrderByDescending(a => a.Severity).ToList()
        };
    }
}

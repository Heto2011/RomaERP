using RomaERP.Application.Alerts.DTOs;

namespace RomaERP.Application.Alerts.Services;

public interface IAlertsService
{
    Task<AlertsReportDto> GetAlertsAsync(CancellationToken ct = default);
}

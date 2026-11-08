using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IDailySalesService
{
    Task<DailySalesEntryDto> CreateAsync(CreateDailySalesDto dto, CancellationToken ct = default);
    Task<List<DailySalesEntryDto>> GetRecentAsync(int take, CancellationToken ct = default);
}

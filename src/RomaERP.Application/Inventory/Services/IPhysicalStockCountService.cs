using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

public interface IPhysicalStockCountService
{
    Task<List<PhysicalStockCountDto>> GetAllAsync(CancellationToken ct = default);
    Task<PhysicalStockCountDto> CreateAsync(CreatePhysicalStockCountDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PhysicalStockCountDto> ApplyAsync(Guid id, ApproveCountDto decision, string? userName, CancellationToken ct = default);
    Task<PhysicalStockCountDto> RejectAsync(Guid id, string? note, string? userName, CancellationToken ct = default);
    Task<int> CountPendingAsync(CancellationToken ct = default);
    /// <summary>Count differences per branch and item over a period: how much was short or over and what it is worth, biggest losses first.</summary>
    Task<List<CountVarianceRowDto>> GetVarianceReportAsync(DateTime? from, DateTime? to, Guid? warehouseId, CancellationToken ct = default);
}

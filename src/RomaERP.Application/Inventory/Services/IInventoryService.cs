using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

public interface IInventoryService
{
    Task<List<StockMovementDto>> GetMovementsAsync(CancellationToken ct = default);
    Task<StockMovementDto> ReceiveStockAsync(ReceiveStockDto dto, CancellationToken ct = default);
    Task<StockMovementDto> IssueStockAsync(IssueStockDto dto, CancellationToken ct = default);
}

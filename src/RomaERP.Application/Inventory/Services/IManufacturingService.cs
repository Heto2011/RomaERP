using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

/// <summary>Internal production: converting raw materials into a semi-finished item (e.g. a sauce made in
/// 20kg batches) so it can then be used as an ingredient inside a menu item's recipe like any other stocked
/// item. Pure inventory transformation — no GL posting, same as Purchase Receiving; the produced item's cost
/// is only recognized as COGS later, when a menu item using it is actually sold.</summary>
public interface IManufacturingService
{
    Task<List<ManufacturingBomDto>> GetBomsAsync(CancellationToken ct = default);
    Task<ManufacturingBomDto?> GetBomByOutputItemAsync(Guid outputItemId, CancellationToken ct = default);
    Task<ManufacturingBomDto> SetBomAsync(Guid outputItemId, SetManufacturingBomDto dto, CancellationToken ct = default);
    Task DeleteBomAsync(Guid outputItemId, CancellationToken ct = default);

    Task<List<ManufacturingOrderDto>> GetOrdersAsync(CancellationToken ct = default);
    Task<ManufacturingOrderDto> CreateOrderAsync(CreateManufacturingOrderDto dto, CancellationToken ct = default);

    Task<List<ManufacturingSiteDto>> GetSitesAsync(CancellationToken ct = default);
    Task<ManufacturingSiteDto> SetSiteAsync(Guid warehouseId, SetManufacturingSiteDto dto, CancellationToken ct = default);
    Task<List<OverheadAbsorptionRowDto>> GetOverheadReportAsync(int year, int month, CancellationToken ct = default);
}

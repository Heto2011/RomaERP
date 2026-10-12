using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

public interface IRestaurantService
{
    Task<List<RestaurantTableDto>> GetTablesAsync(CancellationToken ct = default);
    Task<RestaurantTableDto> CreateTableAsync(CreateRestaurantTableDto dto, CancellationToken ct = default);
    Task<RestaurantTableDto> SetTableStatusAsync(Guid tableId, RestaurantTableStatus status, CancellationToken ct = default);

    Task<List<MenuItemDto>> GetMenuAsync(CancellationToken ct = default);
    Task<List<RecipeLineDto>> GetRecipeAsync(Guid itemId, CancellationToken ct = default);
    Task SetMenuItemAsync(Guid itemId, SetMenuItemDto dto, CancellationToken ct = default);

    Task<List<RestaurantOrderDto>> GetOrdersAsync(bool includeClosed = false, CancellationToken ct = default);
    Task<RestaurantOrderDto> GetOrderAsync(Guid id, CancellationToken ct = default);
    Task<RestaurantOrderDto> CreateOrderAsync(CreateRestaurantOrderDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> AddLineAsync(Guid orderId, AddOrderLineDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> UpdateLineQuantityAsync(Guid orderId, Guid lineId, UpdateOrderLineQuantityDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken ct = default);
    Task<RestaurantOrderDto> SetLineDiscountAsync(Guid orderId, Guid lineId, SetLineDiscountDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> SetOrderDiscountAsync(Guid orderId, SetOrderDiscountDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> SetLineKitchenStatusAsync(Guid orderId, Guid lineId, SetLineKitchenStatusDto dto, CancellationToken ct = default);
    Task<SplitOrderResultDto> SplitOrderAsync(Guid orderId, SplitOrderDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> MoveOrderToTableAsync(Guid orderId, MoveOrderTableDto dto, CancellationToken ct = default);
    Task<List<OfflineOrderResultDto>> SyncOfflineOrdersAsync(OfflineOrdersBatchDto batch, CancellationToken ct = default);
    Task<RestaurantOrderDto> SetOrderLoyaltyAsync(Guid orderId, SetOrderLoyaltyDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> ClearOrderLoyaltyAsync(Guid orderId, CancellationToken ct = default);
    Task<RestaurantOrderDto> MergeOrdersAsync(Guid targetOrderId, MergeOrdersDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> CancelOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<RestaurantOrderDto> BillOrderAsync(Guid orderId, BillOrderDto dto, CancellationToken ct = default);
    Task<RestaurantOrderDto> VoidOrderAsync(Guid orderId, VoidOrderDto dto, CancellationToken ct = default);
}

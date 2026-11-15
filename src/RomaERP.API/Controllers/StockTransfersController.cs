using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;

namespace RomaERP.API.Controllers;

public record ReceiveTransferRequest(string? Note);

/// <summary>Branch-to-branch transfers: "Out" at the sending branch, "In" at the receiving one. No effect on the books.</summary>
[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/inventory")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transfers;
    private readonly ICurrentUserService _user;

    public StockTransfersController(IStockTransferService transfers, ICurrentUserService user)
    {
        _transfers = transfers;
        _user = user;
    }

    private string? Who => _user.UserName ?? _user.UserId;

    [HttpGet("transfers")]
    public async Task<ActionResult<List<StockTransferDto>>> GetAll([FromQuery] StockTransferStatus? status, CancellationToken ct)
        => Ok(await _transfers.GetAllAsync(status, ct));

    [HttpGet("transfers/{id:guid}")]
    public async Task<ActionResult<StockTransferDto>> Get(Guid id, CancellationToken ct) => Ok(await _transfers.GetAsync(id, ct));

    [HttpPost("transfers")]
    public async Task<ActionResult<StockTransferDto>> Send(CreateStockTransferDto dto, CancellationToken ct)
        => Ok(await _transfers.SendAsync(dto, Who, ct));

    [HttpPost("transfers/{id:guid}/receive")]
    public async Task<ActionResult<StockTransferDto>> Receive(Guid id, ReceiveTransferRequest request, CancellationToken ct)
        => Ok(await _transfers.ReceiveAsync(id, request.Note, Who, ct));

    [HttpPost("transfers/{id:guid}/cancel")]
    public async Task<ActionResult<StockTransferDto>> Cancel(Guid id, CancellationToken ct) => Ok(await _transfers.CancelAsync(id, Who, ct));

    [HttpGet("warehouse-stock")]
    public async Task<ActionResult<List<WarehouseStockRowDto>>> WarehouseStock([FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _transfers.GetWarehouseStockAsync(warehouseId, ct));
}

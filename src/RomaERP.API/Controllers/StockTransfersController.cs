using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;

namespace RomaERP.API.Controllers;

public record BranchStockSettingRequest(bool Enforced);

/// <summary>Branch-to-branch transfers: "Out" at the sending branch, "In" at the receiving one. No effect on the books.</summary>
[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/inventory")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transfers;
    private readonly IBranchStockService _branchStock;
    private readonly IStockAdjustmentService _adjustments;
    private readonly ICurrentUserService _user;

    public StockTransfersController(IStockTransferService transfers, IBranchStockService branchStock, IStockAdjustmentService adjustments, ICurrentUserService user)
    {
        _adjustments = adjustments;
        _branchStock = branchStock;
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
    public async Task<ActionResult<StockTransferDto>> Receive(Guid id, ReceiveStockTransferDto request, CancellationToken ct)
        => Ok(await _transfers.ReceiveAsync(id, request, Who, ct));

    [HttpPost("transfers/{id:guid}/cancel")]
    public async Task<ActionResult<StockTransferDto>> Cancel(Guid id, CancellationToken ct) => Ok(await _transfers.CancelAsync(id, Who, ct));

    /// <summary>Whether selling or issuing an item needs the branch itself to hold enough of it (not just the company).</summary>
    [HttpGet("branch-stock-setting")]
    public async Task<ActionResult> GetBranchStockSetting(CancellationToken ct) => Ok(new { enforced = await _branchStock.IsEnforcedAsync(ct) });

    [HttpPut("branch-stock-setting")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetBranchStockSetting(BranchStockSettingRequest request, CancellationToken ct)
    {
        await _branchStock.SetEnforcedAsync(request.Enforced, ct);
        return Ok(new { enforced = request.Enforced });
    }

    [HttpGet("adjustments")]
    public async Task<ActionResult<List<StockAdjustmentDto>>> GetAdjustments(CancellationToken ct) => Ok(await _adjustments.GetAllAsync(ct));

    /// <summary>Corrects an item's stock in a branch (up or down) with a reason, posting its own journal entry.</summary>
    [HttpPost("adjustments")]
    public async Task<ActionResult<StockAdjustmentDto>> CreateAdjustment(CreateStockAdjustmentDto dto, CancellationToken ct)
        => Ok(await _adjustments.CreateAsync(dto, Who, ct));

    [HttpGet("warehouse-stock")]
    public async Task<ActionResult<List<WarehouseStockRowDto>>> WarehouseStock([FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _transfers.GetWarehouseStockAsync(warehouseId, ct));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.POSPolicy)]
[Route("api/restaurant/loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly ILoyaltyService _service;

    public LoyaltyController(ILoyaltyService service)
    {
        _service = service;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<LoyaltySettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _service.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<LoyaltySettingsDto>> UpdateSettings(LoyaltySettingsDto dto, CancellationToken ct)
        => Ok(await _service.UpdateSettingsAsync(dto, ct));

    [HttpGet("members")]
    public async Task<ActionResult<List<LoyaltyMemberDto>>> GetMembers([FromQuery] string? search, CancellationToken ct)
        => Ok(await _service.GetMembersAsync(search, ct));

    [HttpGet("members/by-phone")]
    public async Task<ActionResult<LoyaltyMemberDto>> FindByPhone([FromQuery] string phone, CancellationToken ct)
    {
        var member = await _service.FindByPhoneAsync(phone, ct);
        return member is null ? NotFound() : Ok(member);
    }

    [HttpGet("members/{id:guid}")]
    public async Task<ActionResult<LoyaltyMemberDetailDto>> GetMember(Guid id, CancellationToken ct)
        => Ok(await _service.GetMemberAsync(id, ct));

    [HttpPost("members")]
    public async Task<ActionResult<LoyaltyMemberDto>> CreateMember(SaveLoyaltyMemberDto dto, CancellationToken ct)
        => Ok(await _service.CreateMemberAsync(dto, ct));

    [HttpPut("members/{id:guid}")]
    public async Task<ActionResult<LoyaltyMemberDto>> UpdateMember(Guid id, SaveLoyaltyMemberDto dto, CancellationToken ct)
        => Ok(await _service.UpdateMemberAsync(id, dto, ct));

    [HttpPost("members/{id:guid}/adjust")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<LoyaltyMemberDto>> AdjustPoints(Guid id, AdjustLoyaltyPointsDto dto, CancellationToken ct)
        => Ok(await _service.AdjustPointsAsync(id, dto, ct));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.SalesPolicy)]
[Route("api/customer-followups")]
public class CustomerFollowUpsController : ControllerBase
{
    private readonly ICustomerFollowUpService _service;

    public CustomerFollowUpsController(ICustomerFollowUpService service)
    {
        _service = service;
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<List<OverdueCustomerDto>>> Overdue(CancellationToken ct)
        => Ok(await _service.GetOverdueAsync(null, ct));

    [HttpGet("{customerId:guid}")]
    public async Task<ActionResult<List<FollowUpDto>>> History(Guid customerId, CancellationToken ct)
        => Ok(await _service.GetFollowUpsAsync(customerId, ct));

    [HttpPost]
    public async Task<ActionResult<FollowUpDto>> Add(CreateFollowUpDto dto, CancellationToken ct)
        => Ok(await _service.AddFollowUpAsync(dto, ct));

    [HttpPost("{customerId:guid}/email-reminder")]
    public async Task<ActionResult<ReminderEmailResultDto>> EmailReminder(Guid customerId, CancellationToken ct)
        => Ok(await _service.SendReminderEmailAsync(customerId, ct));
}

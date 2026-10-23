using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Assistant.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/recurring-expenses")]
public class RecurringExpensesController : ControllerBase
{
    private readonly IRecurringExpenseService _service;

    public RecurringExpensesController(IRecurringExpenseService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<RecurringExpenseDto>>> GetAll(CancellationToken ct) => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RecurringExpenseDto>> Create(RecurringExpenseDto dto, CancellationToken ct) => Ok(await _service.SaveAsync(null, dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RecurringExpenseDto>> Update(Guid id, RecurringExpenseDto dto, CancellationToken ct) => Ok(await _service.SaveAsync(id, dto, ct));

    public record ActiveRequest(bool IsActive);

    [HttpPost("{id:guid}/active")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RecurringExpenseDto>> SetActive(Guid id, ActiveRequest request, CancellationToken ct) => Ok(await _service.SetActiveAsync(id, request.IsActive, ct));

    /// <summary>Marks an expense that was just captured as a fixed monthly item.</summary>
    [HttpPost("from-capture/{captureId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RecurringExpenseDto>> FromCapture(Guid captureId, MarkRecurringDto dto, CancellationToken ct)
        => Ok(await _service.MarkFromCaptureAsync(captureId, dto, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

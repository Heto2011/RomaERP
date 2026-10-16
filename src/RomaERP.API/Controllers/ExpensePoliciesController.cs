using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Assistant.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/expense-policies")]
public class ExpensePoliciesController : ControllerBase
{
    private readonly IExpensePolicyService _policies;

    public ExpensePoliciesController(IExpensePolicyService policies) => _policies = policies;

    [HttpGet]
    public async Task<ActionResult<List<ExpensePolicyDto>>> GetAll(CancellationToken ct) => Ok(await _policies.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ExpensePolicyDto>> Create(ExpensePolicyDto dto, CancellationToken ct) => Ok(await _policies.SaveAsync(null, dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ExpensePolicyDto>> Update(Guid id, ExpensePolicyDto dto, CancellationToken ct) => Ok(await _policies.SaveAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _policies.DeleteAsync(id, ct);
        return NoContent();
    }
}

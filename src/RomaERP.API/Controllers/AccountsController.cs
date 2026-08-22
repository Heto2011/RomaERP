using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("tree")]
    public async Task<ActionResult<List<AccountDto>>> GetTree(CancellationToken ct)
        => Ok(await _accountService.GetTreeAsync(ct));

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll(CancellationToken ct)
        => Ok(await _accountService.GetFlatListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _accountService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<AccountDto>> Create(CreateAccountDto dto, CancellationToken ct)
    {
        var result = await _accountService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, UpdateAccountDto dto, CancellationToken ct)
        => Ok(await _accountService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _accountService.DeleteAsync(id, ct);
        return NoContent();
    }
}

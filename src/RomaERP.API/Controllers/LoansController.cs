using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/[controller]")]
public class LoansController : ControllerBase
{
    private readonly ILoanService _service;

    public LoansController(ILoanService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<LoanDto>>> GetAll(CancellationToken ct) => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<LoanDto>> Create(CreateLoanDto dto, CancellationToken ct) => Ok(await _service.CreateAsync(dto, ct));

    [HttpPost("{id:guid}/settle")]
    public async Task<ActionResult<LoanDto>> Settle(Guid id, SettleLoanDto dto, CancellationToken ct) => Ok(await _service.SettleAsync(id, dto, ct));

    [HttpPost("installments/{id:guid}/pay")]
    public async Task<ActionResult<LoanDto>> Pay(Guid id, PayInstallmentDto dto, CancellationToken ct) => Ok(await _service.PayInstallmentAsync(id, dto, ct));
}

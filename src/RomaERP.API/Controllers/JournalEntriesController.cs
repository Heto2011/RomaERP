using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class JournalEntriesController : ControllerBase
{
    private readonly IJournalEntryService _journalEntryService;

    public JournalEntriesController(IJournalEntryService journalEntryService)
    {
        _journalEntryService = journalEntryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<JournalEntryDto>>> GetAll(CancellationToken ct)
        => Ok(await _journalEntryService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JournalEntryDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _journalEntryService.GetByIdAsync(id, ct));

    [HttpGet("trial-balance")]
    public async Task<ActionResult<List<TrialBalanceLineDto>>> GetTrialBalance([FromQuery] DateTime? asOfDate, CancellationToken ct)
        => Ok(await _journalEntryService.GetTrialBalanceAsync(asOfDate, ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<JournalEntryDto>> Create(CreateJournalEntryDto dto, CancellationToken ct)
    {
        var result = await _journalEntryService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/post")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<JournalEntryDto>> Post(Guid id, CancellationToken ct)
        => Ok(await _journalEntryService.PostAsync(id, ct));

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<JournalEntryDto>> Reverse(Guid id, CancellationToken ct)
        => Ok(await _journalEntryService.ReverseAsync(id, ct));
}

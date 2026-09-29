using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public LookupsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("fiscal-periods")]
    public async Task<ActionResult<List<FiscalPeriodLookupDto>>> GetFiscalPeriods(CancellationToken ct)
    {
        var periods = await _context.FiscalPeriods
            .AsNoTracking()
            .OrderBy(p => p.StartDate)
            .Select(p => new FiscalPeriodLookupDto(p.Id, p.Name, p.StartDate, p.EndDate, p.IsClosed))
            .ToListAsync(ct);

        return Ok(periods);
    }

    [HttpGet("cost-centers")]
    public async Task<ActionResult<List<CostCenterLookupDto>>> GetCostCenters(CancellationToken ct)
    {
        var costCenters = await _context.CostCenters
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new CostCenterLookupDto(c.Id, c.Code, c.NameAr))
            .ToListAsync(ct);

        return Ok(costCenters);
    }

    [HttpGet("company-settings")]
    public async Task<ActionResult<CompanySettingsLookupDto>> GetCompanySettings(CancellationToken ct)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return Ok(new CompanySettingsLookupDto(settings?.VatRate ?? 0, settings?.DefaultCurrency ?? "EGP"));
    }
}

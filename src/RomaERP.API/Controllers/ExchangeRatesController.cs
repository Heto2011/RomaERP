using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/exchange-rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRatesController(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ExchangeRateDto>>> GetAll(CancellationToken ct)
        => Ok(await _exchangeRateService.GetRatesAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ExchangeRateDto>> Set(SetExchangeRateDto dto, CancellationToken ct)
        => Ok(await _exchangeRateService.SetRateAsync(dto, ct));
}

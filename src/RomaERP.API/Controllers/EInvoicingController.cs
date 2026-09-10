using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.EInvoicing.DTOs;
using RomaERP.Application.EInvoicing.Services;
using RomaERP.Application.EInvoicing.Services.Zatca;

namespace RomaERP.API.Controllers;

public record RequestZatcaComplianceCsidRequest(string Otp);

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/einvoicing")]
public class EInvoicingController : ControllerBase
{
    private readonly IEInvoicingService _eInvoicingService;
    private readonly IZatcaOnboardingService _zatcaOnboardingService;

    public EInvoicingController(IEInvoicingService eInvoicingService, IZatcaOnboardingService zatcaOnboardingService)
    {
        _eInvoicingService = eInvoicingService;
        _zatcaOnboardingService = zatcaOnboardingService;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<EInvoicingSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _eInvoicingService.GetSettingsAsync(ct));

    [HttpPut("settings")]
    public async Task<ActionResult<EInvoicingSettingsDto>> UpdateSettings(UpdateEInvoicingSettingsDto dto, CancellationToken ct)
        => Ok(await _eInvoicingService.UpdateSettingsAsync(dto, ct));

    [HttpGet("zatca/onboarding")]
    public async Task<ActionResult<ZatcaOnboardingStatusDto>> GetZatcaOnboardingStatus(CancellationToken ct)
        => Ok(await _zatcaOnboardingService.GetStatusAsync(ct));

    [HttpPost("zatca/onboarding/csr")]
    public async Task<ActionResult<ZatcaOnboardingStatusDto>> GenerateZatcaCsr(SaveZatcaOnboardingDetailsDto dto, CancellationToken ct)
        => Ok(await _zatcaOnboardingService.SaveDetailsAndGenerateCsrAsync(dto, ct));

    [HttpPost("zatca/onboarding/compliance-csid")]
    public async Task<ActionResult<ZatcaOnboardingStatusDto>> RequestZatcaComplianceCsid(RequestZatcaComplianceCsidRequest request, CancellationToken ct)
        => Ok(await _zatcaOnboardingService.RequestComplianceCsidAsync(request.Otp, ct));

    [HttpPost("zatca/onboarding/compliance-checks")]
    public async Task<ActionResult<ZatcaOnboardingStatusDto>> RunZatcaComplianceChecks(CancellationToken ct)
        => Ok(await _zatcaOnboardingService.RunComplianceChecksAsync(ct));

    [HttpPost("zatca/onboarding/production-csid")]
    public async Task<ActionResult<ZatcaOnboardingStatusDto>> RequestZatcaProductionCsid(CancellationToken ct)
        => Ok(await _zatcaOnboardingService.RequestProductionCsidAsync(ct));
}

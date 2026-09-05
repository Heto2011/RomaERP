using RomaERP.Domain.EInvoicing;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

public class SaveZatcaOnboardingDetailsDto
{
    public required string OrganizationIdentifier { get; set; }
    public required string SolutionName { get; set; }
    public required string Model { get; set; }
    public required string DeviceSerialNumber { get; set; }
    public required string OrganizationUnitName { get; set; }
    public required string Address { get; set; }
    public required string BusinessCategory { get; set; }
    public string InvoiceType { get; set; } = "1100";
}

public class ZatcaOnboardingStatusDto
{
    public ZatcaOnboardingStage Stage { get; set; }
    public bool HasCsr { get; set; }
    public string? ComplianceRequestId { get; set; }
    public bool HasCertificate { get; set; }
    public bool HasSecret { get; set; }
    public string? LastComplianceCheckStatus { get; set; }
}

/// <summary>Drives ZATCA's 4-step CSID onboarding wizard — the same "Create CSR → Request Compliance CSID →
/// Complete Compliance Checks → Request Production CSID" flow every ZATCA-integrated system walks a taxpayer
/// through (Odoo's l10n_sa_edi module included). See
/// RomaERP.Infrastructure.EInvoicing.Zatca.ZatcaOnboardingService for the real implementation and its
/// caveats — this session has no network access to ZATCA's real servers to verify any of it end-to-end.</summary>
public interface IZatcaOnboardingService
{
    Task<ZatcaOnboardingStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<ZatcaOnboardingStatusDto> SaveDetailsAndGenerateCsrAsync(SaveZatcaOnboardingDetailsDto dto, CancellationToken ct = default);
    Task<ZatcaOnboardingStatusDto> RequestComplianceCsidAsync(string otp, CancellationToken ct = default);
    Task<ZatcaOnboardingStatusDto> RunComplianceChecksAsync(CancellationToken ct = default);
    Task<ZatcaOnboardingStatusDto> RequestProductionCsidAsync(CancellationToken ct = default);
}

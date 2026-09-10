namespace RomaERP.Domain.EInvoicing;

public enum EInvoicingProvider
{
    None = 0,
    Eta = 1,
    Zatca = 2
}

public enum EInvoicingEnvironment
{
    Sandbox = 1,
    Production = 2,
    /// <summary>ZATCA only: the pre-production "simulation" tier, between sandbox and production.</summary>
    Simulation = 3
}

public enum EInvoiceStatus
{
    NotSubmitted = 1,
    Submitted = 2,
    Accepted = 3,
    Rejected = 4
}

/// <summary>ZATCA's 4-step CSID onboarding flow (mirrors the "Create CSR → Request Compliance CSID →
/// Complete Compliance Checks → Request Production CSID" wizard every ZATCA-integrated system, including
/// Odoo's own l10n_sa_edi module, walks a taxpayer through).</summary>
public enum ZatcaOnboardingStage
{
    NotStarted = 1,
    CsrGenerated = 2,
    ComplianceCsidObtained = 3,
    ComplianceChecksPassed = 4,
    ProductionCsidObtained = 5
}

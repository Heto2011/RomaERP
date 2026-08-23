using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.EInvoicing.Zatca;

/// <summary>
/// Real implementation of ZATCA's 4-step CSID onboarding wizard. Each step is a thin orchestration over
/// ZatcaCertificateBuilder (CSR generation) and ZatcaHttpApiClient (the actual network calls) — see both for
/// their own caveats. This has NOT been exercised against a real ZATCA environment: this sandbox has no
/// network access to zatca.gov.sa, so every step beyond CSR generation can only be considered "correctly
/// wired" (compiles, sends the right shape) rather than "confirmed working."
///
/// The compliance-checks step (RunComplianceChecksAsync) is a known simplification: ZATCA's official
/// compliance suite exercises 6 document types (standard invoice, standard credit note, standard debit note,
/// simplified invoice, simplified credit note, simplified debit note). RomaERP doesn't model credit/debit
/// notes yet, so this only submits two synthetic sample invoices (one Standard/B2B-shaped, one
/// Simplified/B2C-shaped) built in-memory rather than from real data. Add credit/debit note support and wire
/// the remaining 4 document types before treating a "PassedComplianceChecks" result as equivalent to ZATCA's
/// own compliance sign-off.
/// </summary>
public class ZatcaOnboardingService : IZatcaOnboardingService
{
    private readonly IApplicationDbContext _context;
    private readonly ISecretProtector _secretProtector;
    private readonly IZatcaApiClient _apiClient;
    private readonly IZatcaDocumentSigner _signer;

    public ZatcaOnboardingService(IApplicationDbContext context, ISecretProtector secretProtector, IZatcaApiClient apiClient, IZatcaDocumentSigner signer)
    {
        _context = context;
        _secretProtector = secretProtector;
        _apiClient = apiClient;
        _signer = signer;
    }

    public async Task<ZatcaOnboardingStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return Map(settings);
    }

    public async Task<ZatcaOnboardingStatusDto> SaveDetailsAndGenerateCsrAsync(SaveZatcaOnboardingDetailsDto dto, CancellationToken ct = default)
    {
        var settings = await LoadTrackedSettingsAsync(ct);

        settings.EInvoicingZatcaOrganizationIdentifier = dto.OrganizationIdentifier;
        settings.EInvoicingZatcaSolutionName = dto.SolutionName;
        settings.EInvoicingZatcaModel = dto.Model;
        settings.EInvoicingZatcaDeviceSerialNumber = dto.DeviceSerialNumber;
        settings.EInvoicingZatcaOrganizationUnitName = dto.OrganizationUnitName;
        settings.EInvoicingZatcaAddress = dto.Address;
        settings.EInvoicingZatcaBusinessCategory = dto.BusinessCategory;
        settings.EInvoicingZatcaInvoiceType = dto.InvoiceType;

        var csrResult = ZatcaCertificateBuilder.Generate(new ZatcaCsrOptions
        {
            OrganizationIdentifier = dto.OrganizationIdentifier,
            SolutionName = dto.SolutionName,
            Model = dto.Model,
            DeviceSerialNumber = dto.DeviceSerialNumber,
            CommonName = settings.CompanyNameEn,
            OrganizationName = settings.CompanyNameEn,
            OrganizationalUnitName = dto.OrganizationUnitName,
            Address = dto.Address,
            InvoiceType = dto.InvoiceType,
            BusinessCategory = dto.BusinessCategory,
            Environment = settings.EInvoicingEnvironment,
        });

        settings.EInvoicingZatcaCsrPem = csrResult.CsrPem;
        settings.EInvoicingPrivateKeyEncrypted = _secretProtector.Protect(csrResult.PrivateKeyPem);
        settings.EInvoicingZatcaOnboardingStage = ZatcaOnboardingStage.CsrGenerated;
        // A fresh CSR invalidates any previously obtained CSID.
        settings.EInvoicingCertificateEncrypted = null;
        settings.EInvoicingClientSecretEncrypted = null;
        settings.EInvoicingZatcaComplianceRequestId = null;

        await _context.SaveChangesAsync(ct);
        return Map(settings);
    }

    public async Task<ZatcaOnboardingStatusDto> RequestComplianceCsidAsync(string otp, CancellationToken ct = default)
    {
        var settings = await LoadTrackedSettingsAsync(ct);
        if (settings.EInvoicingZatcaOnboardingStage < ZatcaOnboardingStage.CsrGenerated || string.IsNullOrEmpty(settings.EInvoicingZatcaCsrPem))
            throw new ValidationAppException("لازم تعمل طلب الشهادة (CSR) الأول قبل طلب شهادة المطابقة.");

        var result = await _apiClient.RequestComplianceCertificateAsync(settings.EInvoicingZatcaCsrPem, otp, settings, ct);

        settings.EInvoicingCertificateEncrypted = _secretProtector.Protect(result.CertificatePem);
        settings.EInvoicingClientSecretEncrypted = _secretProtector.Protect(result.Secret);
        settings.EInvoicingZatcaComplianceRequestId = result.RequestId;
        settings.EInvoicingZatcaOnboardingStage = ZatcaOnboardingStage.ComplianceCsidObtained;

        await _context.SaveChangesAsync(ct);
        return Map(settings);
    }

    public async Task<ZatcaOnboardingStatusDto> RunComplianceChecksAsync(CancellationToken ct = default)
    {
        var settings = await LoadTrackedSettingsAsync(ct);
        if (settings.EInvoicingZatcaOnboardingStage < ZatcaOnboardingStage.ComplianceCsidObtained
            || string.IsNullOrEmpty(settings.EInvoicingCertificateEncrypted) || string.IsNullOrEmpty(settings.EInvoicingClientSecretEncrypted))
            throw new ValidationAppException("لازم تطلب شهادة المطابقة (Compliance CSID) الأول قبل تشغيل اختبارات المطابقة.");

        var complianceCertificatePem = _secretProtector.Unprotect(settings.EInvoicingCertificateEncrypted);
        var complianceSecret = _secretProtector.Unprotect(settings.EInvoicingClientSecretEncrypted);

        var sampleInvoices = BuildSampleInvoices();
        string? pih = null;
        var icv = 0;
        var lastStatus = "PASS";

        foreach (var (invoice, customer) in sampleInvoices)
        {
            icv++;
            var (unsignedDocument, _) = ZatcaInvoiceDocumentBuilder.Build(
                invoice, customer, settings, icv, pih ?? RomaERP.Application.EInvoicing.Services.Zatca.ZatcaEInvoicingProvider.FirstInvoicePih);
            var unsignedXml = unsignedDocument.ToString(SaveOptions.DisableFormatting);
            var signingResult = await _signer.SignInvoiceXmlAsync(unsignedXml, settings, ct);
            pih = signingResult.InvoiceHash;

            var checkResult = await _apiClient.ValidateComplianceInvoiceAsync(
                complianceCertificatePem, complianceSecret, signingResult.SignedXml, signingResult.InvoiceHash, signingResult.Uuid, settings, ct);

            if (!checkResult.Success)
                throw new ValidationAppException($"فشل اختبار المطابقة على فاتورة تجريبية ({(customer.TaxRegistrationNumber is null ? "مبسّطة" : "عادية")}): {checkResult.ErrorMessage}");

            lastStatus = checkResult.Status ?? lastStatus;
        }

        settings.EInvoicingZatcaOnboardingStage = ZatcaOnboardingStage.ComplianceChecksPassed;
        await _context.SaveChangesAsync(ct);

        var dto = Map(settings);
        dto.LastComplianceCheckStatus = lastStatus;
        return dto;
    }

    public async Task<ZatcaOnboardingStatusDto> RequestProductionCsidAsync(CancellationToken ct = default)
    {
        var settings = await LoadTrackedSettingsAsync(ct);
        if (settings.EInvoicingZatcaOnboardingStage < ZatcaOnboardingStage.ComplianceChecksPassed
            || string.IsNullOrEmpty(settings.EInvoicingCertificateEncrypted) || string.IsNullOrEmpty(settings.EInvoicingClientSecretEncrypted)
            || string.IsNullOrEmpty(settings.EInvoicingZatcaComplianceRequestId))
            throw new ValidationAppException("لازم تخلّص اختبارات المطابقة الأول قبل طلب الشهادة الإنتاجية.");

        var complianceCertificatePem = _secretProtector.Unprotect(settings.EInvoicingCertificateEncrypted);
        var complianceSecret = _secretProtector.Unprotect(settings.EInvoicingClientSecretEncrypted);

        var result = await _apiClient.RequestProductionCertificateAsync(complianceCertificatePem, complianceSecret, settings.EInvoicingZatcaComplianceRequestId, settings, ct);

        // Replace the active certificate/secret with the production CSID — from this point on, real invoice
        // submissions (ZatcaEInvoicingProvider) use these, not the compliance-phase ones.
        settings.EInvoicingCertificateEncrypted = _secretProtector.Protect(result.CertificatePem);
        settings.EInvoicingClientSecretEncrypted = _secretProtector.Protect(result.Secret);
        settings.EInvoicingZatcaOnboardingStage = ZatcaOnboardingStage.ProductionCsidObtained;
        // Reset the invoice hash chain — production is a fresh ICV/PIH sequence, separate from compliance-test invoices.
        settings.EInvoicingSubmittedCount = 0;
        settings.EInvoicingLastInvoiceHash = null;

        await _context.SaveChangesAsync(ct);
        return Map(settings);
    }

    private async Task<CompanySettings> LoadTrackedSettingsAsync(CancellationToken ct)
        => await _context.CompanySettings.FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(CompanySettings), Guid.Empty);

    private static List<(SalesInvoice Invoice, Customer Customer)> BuildSampleInvoices()
    {
        var standardCustomer = new Customer { Id = Guid.NewGuid(), Code = "COMPLIANCE-B2B", NameAr = "عميل تجريبي (عادية)", NameEn = "Compliance Test Standard", TaxRegistrationNumber = "399999999900003" };
        var simplifiedCustomer = new Customer { Id = Guid.NewGuid(), Code = "COMPLIANCE-B2C", NameAr = "عميل تجريبي (مبسّطة)", NameEn = "Compliance Test Simplified" };

        SalesInvoice BuildInvoice(string number, Customer customer)
        {
            const decimal subTotal = 100m;
            const decimal vatRate = 0.15m;
            var vat = subTotal * vatRate;
            return new SalesInvoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = number,
                InvoiceDate = DateTime.UtcNow,
                Customer = customer,
                CustomerId = customer.Id,
                SubTotal = subTotal,
                VatRate = vatRate,
                VatAmount = vat,
                TotalAmount = subTotal + vat,
                Lines = new List<SalesInvoiceLine>
                {
                    new() { LineNumber = 1, Description = "Compliance test item", Quantity = 1, UnitPrice = subTotal, LineTotal = subTotal }
                }
            };
        }

        return new List<(SalesInvoice, Customer)>
        {
            (BuildInvoice("COMPLIANCE-STD-1", standardCustomer), standardCustomer),
            (BuildInvoice("COMPLIANCE-SIM-1", simplifiedCustomer), simplifiedCustomer),
        };
    }

    private static ZatcaOnboardingStatusDto Map(CompanySettings? settings) => new()
    {
        Stage = settings?.EInvoicingZatcaOnboardingStage ?? ZatcaOnboardingStage.NotStarted,
        HasCsr = !string.IsNullOrEmpty(settings?.EInvoicingZatcaCsrPem),
        ComplianceRequestId = settings?.EInvoicingZatcaComplianceRequestId,
        HasCertificate = !string.IsNullOrEmpty(settings?.EInvoicingCertificateEncrypted),
        HasSecret = !string.IsNullOrEmpty(settings?.EInvoicingClientSecretEncrypted),
    };
}

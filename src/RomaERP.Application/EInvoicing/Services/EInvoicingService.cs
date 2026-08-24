using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.DTOs;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services;

public class EInvoicingService : IEInvoicingService
{
    private readonly IApplicationDbContext _context;
    private readonly ISecretProtector _secretProtector;
    private readonly IEnumerable<IEInvoicingProvider> _providers;

    public EInvoicingService(IApplicationDbContext context, ISecretProtector secretProtector, IEnumerable<IEInvoicingProvider> providers)
    {
        _context = context;
        _secretProtector = secretProtector;
        _providers = providers;
    }

    public async Task<EInvoicingSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return Map(settings);
    }

    public async Task<EInvoicingSettingsDto> UpdateSettingsAsync(UpdateEInvoicingSettingsDto dto, CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CompanySettings), Guid.Empty);

        settings.EInvoicingProvider = dto.Provider;
        settings.EInvoicingEnvironment = dto.Environment;

        if (dto.ClientId is not null)
            settings.EInvoicingClientId = dto.ClientId;
        if (dto.ClientSecret is not null)
            settings.EInvoicingClientSecretEncrypted = _secretProtector.Protect(dto.ClientSecret);
        if (dto.Certificate is not null)
            settings.EInvoicingCertificateEncrypted = _secretProtector.Protect(dto.Certificate);
        if (dto.PrivateKey is not null)
            settings.EInvoicingPrivateKeyEncrypted = _secretProtector.Protect(dto.PrivateKey);

        await _context.SaveChangesAsync(ct);
        return Map(settings);
    }

    public async Task<EInvoiceStatusDto> SubmitInvoiceAsync(Guid salesInvoiceId, CancellationToken ct = default)
    {
        var invoice = await _context.SalesInvoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == salesInvoiceId, ct)
            ?? throw new NotFoundException(nameof(SalesInvoice), salesInvoiceId);

        var settings = await _context.CompanySettings.FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CompanySettings), Guid.Empty);

        if (settings.EInvoicingProvider == EInvoicingProvider.None)
            throw new ValidationAppException("لسه مفيش منظومة فاتورة إلكترونية مفعّلة لهذه الشركة.");

        var provider = _providers.FirstOrDefault(p => p.ProviderType == settings.EInvoicingProvider)
            ?? throw new ValidationAppException("منظومة الفاتورة الإلكترونية المختارة غير مدعومة حاليًا.");

        var result = await provider.SubmitInvoiceAsync(invoice, invoice.Customer!, settings, ct);

        invoice.EInvoiceStatus = result.Success ? EInvoiceStatus.Accepted : EInvoiceStatus.Rejected;
        invoice.EInvoiceExternalUuid = result.ExternalUuid;
        invoice.EInvoiceHash = result.DocumentHash;
        invoice.EInvoiceSubmittedAtUtc = DateTime.UtcNow;
        invoice.EInvoiceErrorMessage = result.ErrorMessage;

        await _context.SaveChangesAsync(ct);

        return new EInvoiceStatusDto
        {
            SalesInvoiceId = invoice.Id,
            Status = invoice.EInvoiceStatus,
            ExternalUuid = invoice.EInvoiceExternalUuid,
            SubmittedAtUtc = invoice.EInvoiceSubmittedAtUtc,
            ErrorMessage = invoice.EInvoiceErrorMessage
        };
    }

    public async Task<EInvoiceNoteStatusDto> SubmitNoteAsync(Guid salesNoteId, CancellationToken ct = default)
    {
        var note = await _context.SalesNotes
            .Include(n => n.Customer)
            .Include(n => n.OriginalInvoice)
            .Include(n => n.Lines)
            .FirstOrDefaultAsync(n => n.Id == salesNoteId, ct)
            ?? throw new NotFoundException(nameof(SalesNote), salesNoteId);

        var settings = await _context.CompanySettings.FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CompanySettings), Guid.Empty);

        if (settings.EInvoicingProvider == EInvoicingProvider.None)
            throw new ValidationAppException("لسه مفيش منظومة فاتورة إلكترونية مفعّلة لهذه الشركة.");

        var provider = _providers.FirstOrDefault(p => p.ProviderType == settings.EInvoicingProvider)
            ?? throw new ValidationAppException("منظومة الفاتورة الإلكترونية المختارة غير مدعومة حاليًا.");

        var result = await provider.SubmitNoteAsync(note, note.Customer!, settings, ct);

        note.EInvoiceStatus = result.Success ? EInvoiceStatus.Accepted : EInvoiceStatus.Rejected;
        note.EInvoiceExternalUuid = result.ExternalUuid;
        note.EInvoiceHash = result.DocumentHash;
        note.EInvoiceSubmittedAtUtc = DateTime.UtcNow;
        note.EInvoiceErrorMessage = result.ErrorMessage;

        await _context.SaveChangesAsync(ct);

        return new EInvoiceNoteStatusDto
        {
            SalesNoteId = note.Id,
            Status = note.EInvoiceStatus,
            ExternalUuid = note.EInvoiceExternalUuid,
            SubmittedAtUtc = note.EInvoiceSubmittedAtUtc,
            ErrorMessage = note.EInvoiceErrorMessage
        };
    }

    private static EInvoicingSettingsDto Map(CompanySettings? settings) => new()
    {
        Provider = settings?.EInvoicingProvider ?? EInvoicingProvider.None,
        Environment = settings?.EInvoicingEnvironment ?? EInvoicingEnvironment.Sandbox,
        HasClientCredentials = !string.IsNullOrEmpty(settings?.EInvoicingClientId) && !string.IsNullOrEmpty(settings?.EInvoicingClientSecretEncrypted),
        HasCertificate = !string.IsNullOrEmpty(settings?.EInvoicingCertificateEncrypted)
    };
}

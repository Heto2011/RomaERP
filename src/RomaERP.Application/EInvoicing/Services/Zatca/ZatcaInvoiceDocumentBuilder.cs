using System.Globalization;
using System.Xml.Linq;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

public enum ZatcaInvoiceType
{
    /// <summary>Buyer has a VAT registration number — cleared via the Clearance API before issuance.</summary>
    Standard,
    /// <summary>Buyer is a walk-in/individual consumer — reported (not cleared) via the Reporting API.</summary>
    Simplified
}

/// <summary>Maps a RomaERP SalesInvoice (or SalesNote) to a ZATCA-compliant UBL 2.1 XML invoice, including the
/// ICV/PIH hash chain as AdditionalDocumentReference elements. Deliberately produces the document WITHOUT the
/// QR code, ext:UBLExtensions, or cac:Signature elements — those depend on the invoice hash and digital
/// signature, which only exist once IZatcaDocumentSigner has processed this "unsigned" document, so it builds
/// the exact shape ZATCA's algorithm hashes (see ZatcaXadesDocumentSigner) rather than a placeholder that would
/// change the hash. The line/party mapping here is this session's best-effort reading of the UBL 2.1 invoice
/// shape; it has not been validated against ZATCA's official XSD.
///
/// Credit/Debit notes use the SAME UBL "Invoice" root element as regular invoices (confirmed against the
/// Saleh7/php-zatca-xml reference implementation's Invoice.php — there is no separate CreditNote/DebitNote XML
/// root in that model) — distinguished only by cbc:InvoiceTypeCode's VALUE (388 invoice / 381 credit note /
/// 383 debit note; the "name" attribute for Standard-vs-Simplified stays orthogonal), plus an added
/// cac:BillingReference/cac:InvoiceDocumentReference/cbc:ID pointing at the original invoice number. Confirmed
/// element order places BillingReference right after TaxCurrencyCode/OrderReference and before the
/// AdditionalDocumentReference (ICV/PIH) elements.</summary>
public static class ZatcaInvoiceDocumentBuilder
{
    public static readonly XNamespace Ns = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private const string InvoiceTypeCodeValue = "388";
    private const string CreditNoteTypeCodeValue = "381";
    private const string DebitNoteTypeCodeValue = "383";

    public static ZatcaInvoiceType DetermineInvoiceType(Customer customer)
        => string.IsNullOrWhiteSpace(customer.TaxRegistrationNumber) ? ZatcaInvoiceType.Simplified : ZatcaInvoiceType.Standard;

    public static (XDocument Document, string Uuid) Build(
        SalesInvoice invoice,
        Customer customer,
        CompanySettings settings,
        int invoiceCounterValue,
        string previousInvoiceHash)
    {
        var lines = invoice.Lines.Select(l => new LineItem(l.Description, l.Quantity, l.UnitPrice, l.LineTotal));

        return BuildDocument(
            documentNumber: invoice.InvoiceNumber,
            documentDate: invoice.InvoiceDate,
            typeCodeValue: InvoiceTypeCodeValue,
            billingReferenceInvoiceNumber: null,
            customer: customer,
            settings: settings,
            subTotal: invoice.SubTotal,
            vatAmount: invoice.VatAmount,
            totalAmount: invoice.TotalAmount,
            invoiceCounterValue: invoiceCounterValue,
            previousInvoiceHash: previousInvoiceHash,
            lines: lines);
    }

    /// <summary>Builds a credit or debit note document. <paramref name="originalInvoiceNumber"/> is the
    /// InvoiceNumber of the SalesInvoice this note references, carried in cac:BillingReference.</summary>
    public static (XDocument Document, string Uuid) BuildNote(
        SalesNote note,
        string originalInvoiceNumber,
        Customer customer,
        CompanySettings settings,
        int invoiceCounterValue,
        string previousInvoiceHash)
    {
        var typeCodeValue = note.NoteType == SalesNoteType.Credit ? CreditNoteTypeCodeValue : DebitNoteTypeCodeValue;
        var lines = note.Lines.Select(l => new LineItem(l.Description, l.Quantity, l.UnitPrice, l.LineTotal));

        return BuildDocument(
            documentNumber: note.NoteNumber,
            documentDate: note.NoteDate,
            typeCodeValue: typeCodeValue,
            billingReferenceInvoiceNumber: originalInvoiceNumber,
            customer: customer,
            settings: settings,
            subTotal: note.SubTotal,
            vatAmount: note.VatAmount,
            totalAmount: note.TotalAmount,
            invoiceCounterValue: invoiceCounterValue,
            previousInvoiceHash: previousInvoiceHash,
            lines: lines);
    }

    private readonly record struct LineItem(string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

    private static (XDocument Document, string Uuid) BuildDocument(
        string documentNumber,
        DateTime documentDate,
        string typeCodeValue,
        string? billingReferenceInvoiceNumber,
        Customer customer,
        CompanySettings settings,
        decimal subTotal,
        decimal vatAmount,
        decimal totalAmount,
        int invoiceCounterValue,
        string previousInvoiceHash,
        IEnumerable<LineItem> lines)
    {
        var uuid = Guid.NewGuid().ToString();
        var invoiceType = DetermineInvoiceType(customer);
        var typeCodeName = invoiceType == ZatcaInvoiceType.Standard ? "0100000" : "0200000";

        var lineElements = lines.Select((line, i) =>
        {
            var lineVat = Math.Round(line.LineTotal * settings.VatRate, 2);
            return new XElement(Cac + "InvoiceLine",
                new XElement(Cbc + "ID", i + 1),
                new XElement(Cbc + "InvoicedQuantity", new XAttribute("unitCode", "PCE"), line.Quantity),
                new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", settings.DefaultCurrency), line.LineTotal),
                new XElement(Cac + "TaxTotal",
                    new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", settings.DefaultCurrency), lineVat),
                    new XElement(Cbc + "RoundingAmount", new XAttribute("currencyID", settings.DefaultCurrency), line.LineTotal + lineVat)),
                new XElement(Cac + "Item",
                    new XElement(Cbc + "Name", line.Description),
                    new XElement(Cac + "ClassifiedTaxCategory",
                        new XElement(Cbc + "ID", "S"),
                        new XElement(Cbc + "Percent", settings.VatRate * 100),
                        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))),
                new XElement(Cac + "Price",
                    new XElement(Cbc + "PriceAmount", new XAttribute("currencyID", settings.DefaultCurrency), line.UnitPrice)));
        });

        XElement? billingReference = billingReferenceInvoiceNumber is null
            ? null
            : new XElement(Cac + "BillingReference",
                new XElement(Cac + "InvoiceDocumentReference",
                    new XElement(Cbc + "ID", billingReferenceInvoiceNumber)));

        var document = new XDocument(
            new XElement(Ns + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", Cac),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
                new XElement(Cbc + "ProfileID", "reporting:1.0"),
                new XElement(Cbc + "ID", documentNumber),
                new XElement(Cbc + "UUID", uuid),
                new XElement(Cbc + "IssueDate", documentDate.ToString("yyyy-MM-dd")),
                new XElement(Cbc + "IssueTime", documentDate.ToString("HH:mm:ss")),
                new XElement(Cbc + "InvoiceTypeCode", new XAttribute("name", typeCodeName), typeCodeValue),
                new XElement(Cbc + "DocumentCurrencyCode", settings.DefaultCurrency),
                new XElement(Cbc + "TaxCurrencyCode", settings.DefaultCurrency),

                billingReference,

                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "ICV"),
                    new XElement(Cbc + "UUID", invoiceCounterValue.ToString(CultureInfo.InvariantCulture))),
                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "PIH"),
                    new XElement(Cac + "Attachment",
                        new XElement(Cbc + "EmbeddedDocumentBinaryObject", new XAttribute("mimeCode", "text/plain"), previousInvoiceHash))),

                new XElement(Cac + "AccountingSupplierParty",
                    new XElement(Cac + "Party",
                        new XElement(Cac + "PartyTaxScheme",
                            new XElement(Cbc + "CompanyID", settings.TaxRegistrationNumber),
                            new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT"))),
                        new XElement(Cac + "PartyLegalEntity",
                            new XElement(Cbc + "RegistrationName", settings.CompanyNameAr)))),
                new XElement(Cac + "AccountingCustomerParty",
                    new XElement(Cac + "Party",
                        invoiceType == ZatcaInvoiceType.Standard
                            ? new XElement(Cac + "PartyTaxScheme",
                                new XElement(Cbc + "CompanyID", customer.TaxRegistrationNumber),
                                new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))
                            : null,
                        new XElement(Cac + "PartyLegalEntity",
                            new XElement(Cbc + "RegistrationName", customer.NameAr)))),

                new XElement(Cac + "TaxTotal",
                    new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", settings.DefaultCurrency), vatAmount)),
                new XElement(Cac + "LegalMonetaryTotal",
                    new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", settings.DefaultCurrency), subTotal),
                    new XElement(Cbc + "TaxExclusiveAmount", new XAttribute("currencyID", settings.DefaultCurrency), subTotal),
                    new XElement(Cbc + "TaxInclusiveAmount", new XAttribute("currencyID", settings.DefaultCurrency), totalAmount),
                    new XElement(Cbc + "PayableAmount", new XAttribute("currencyID", settings.DefaultCurrency), totalAmount)),

                lineElements));

        return (document, uuid);
    }
}

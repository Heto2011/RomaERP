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

/// <summary>Maps a RomaERP SalesInvoice to a ZATCA-compliant UBL 2.1 XML invoice, including the ICV/PIH hash
/// chain and QR code as AdditionalDocumentReference elements. This covers the core required structure —
/// full production compliance (exact XML canonicalization, UBLExtensions placement for the cryptographic
/// stamp, XAdES signature envelope) needs validation against ZATCA's official XSD/validator once a real
/// sandbox account is available; this session had no live ZATCA access to verify against.</summary>
public static class ZatcaInvoiceDocumentBuilder
{
    private static readonly XNamespace Ns = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public static ZatcaInvoiceType DetermineInvoiceType(Customer customer)
        => string.IsNullOrWhiteSpace(customer.TaxRegistrationNumber) ? ZatcaInvoiceType.Simplified : ZatcaInvoiceType.Standard;

    public static (XDocument Document, string Uuid) Build(
        SalesInvoice invoice,
        Customer customer,
        CompanySettings settings,
        int invoiceCounterValue,
        string previousInvoiceHash,
        string qrCodeBase64)
    {
        var uuid = Guid.NewGuid().ToString();
        var invoiceType = DetermineInvoiceType(customer);
        var typeCodeName = invoiceType == ZatcaInvoiceType.Standard ? "0100000" : "0200000";

        var lines = invoice.Lines.Select((line, i) =>
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

        var document = new XDocument(
            new XElement(Ns + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", Cac),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
                new XElement(Cbc + "ProfileID", "reporting:1.0"),
                new XElement(Cbc + "ID", invoice.InvoiceNumber),
                new XElement(Cbc + "UUID", uuid),
                new XElement(Cbc + "IssueDate", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                new XElement(Cbc + "IssueTime", invoice.InvoiceDate.ToString("HH:mm:ss")),
                new XElement(Cbc + "InvoiceTypeCode", new XAttribute("name", typeCodeName), "388"),
                new XElement(Cbc + "DocumentCurrencyCode", settings.DefaultCurrency),
                new XElement(Cbc + "TaxCurrencyCode", settings.DefaultCurrency),

                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "ICV"),
                    new XElement(Cbc + "UUID", invoiceCounterValue.ToString(CultureInfo.InvariantCulture))),
                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "PIH"),
                    new XElement(Cac + "Attachment",
                        new XElement(Cbc + "EmbeddedDocumentBinaryObject", new XAttribute("mimeCode", "text/plain"), previousInvoiceHash))),
                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "QR"),
                    new XElement(Cac + "Attachment",
                        new XElement(Cbc + "EmbeddedDocumentBinaryObject", new XAttribute("mimeCode", "text/plain"), qrCodeBase64))),

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
                    new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", settings.DefaultCurrency), invoice.VatAmount)),
                new XElement(Cac + "LegalMonetaryTotal",
                    new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", settings.DefaultCurrency), invoice.SubTotal),
                    new XElement(Cbc + "TaxExclusiveAmount", new XAttribute("currencyID", settings.DefaultCurrency), invoice.SubTotal),
                    new XElement(Cbc + "TaxInclusiveAmount", new XAttribute("currencyID", settings.DefaultCurrency), invoice.TotalAmount),
                    new XElement(Cbc + "PayableAmount", new XAttribute("currencyID", settings.DefaultCurrency), invoice.TotalAmount)),

                lines));

        return (document, uuid);
    }
}

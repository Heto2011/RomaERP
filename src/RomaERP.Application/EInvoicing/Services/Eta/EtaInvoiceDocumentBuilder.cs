using System.Text.Json.Nodes;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Eta;

/// <summary>Maps a RomaERP SalesInvoice to the ETA (Egyptian Tax Authority) e-invoice JSON document shape.
/// Field names/structure follow ETA's published document schema (issuer/receiver/invoiceLines/taxTotals) —
/// validate against the live schema at https://sdk.invoicing.eta.gov.eg/ before submitting to a real
/// (non-mock) ETA environment, since the authority can revise field-level requirements.</summary>
public static class EtaInvoiceDocumentBuilder
{
    public static JsonObject Build(SalesInvoice invoice, Customer customer, CompanySettings settings)
    {
        var lines = BuildLines(invoice.Lines.Select(l => (l.ItemId, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)), settings);

        return BuildDocument(
            documentType: "I",
            internalId: invoice.InvoiceNumber,
            dateTimeIssued: invoice.InvoiceDate,
            customer: customer,
            settings: settings,
            subTotal: invoice.SubTotal,
            vatAmount: invoice.VatAmount,
            totalAmount: invoice.TotalAmount,
            lines: lines,
            referencedInternalId: null);
    }

    /// <summary>Builds a credit ("C") or debit ("D") note document. LOWER CONFIDENCE than Build(): the
    /// documentType codes and the "references" field for linking back to the original invoice are recalled
    /// from ETA's published SDK docs, not freshly re-verified against a live schema this session (unlike the
    /// ZATCA equivalent, which was cross-checked against the Saleh7/php-zatca-xml reference implementation).
    /// Verify against https://sdk.invoicing.eta.gov.eg/ before relying on this for a real ETA submission.</summary>
    public static JsonObject BuildNote(SalesNote note, string originalInvoiceInternalId, Customer customer, CompanySettings settings)
    {
        var lines = BuildLines(note.Lines.Select(l => ((Guid?)null, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)), settings);
        var documentType = note.NoteType == SalesNoteType.Credit ? "C" : "D";

        return BuildDocument(
            documentType: documentType,
            internalId: note.NoteNumber,
            dateTimeIssued: note.NoteDate,
            customer: customer,
            settings: settings,
            subTotal: note.SubTotal,
            vatAmount: note.VatAmount,
            totalAmount: note.TotalAmount,
            lines: lines,
            referencedInternalId: originalInvoiceInternalId);
    }

    private static JsonArray BuildLines(IEnumerable<(Guid? ItemId, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal)> lines, CompanySettings settings)
    {
        var result = new JsonArray();
        var lineNumber = 1;
        foreach (var line in lines)
        {
            var lineVat = Math.Round(line.LineTotal * settings.VatRate, 2);
            result.Add(new JsonObject
            {
                ["internalCode"] = line.ItemId?.ToString() ?? $"SERVICE-{lineNumber}",
                ["description"] = line.Description,
                ["itemType"] = "EGS",
                ["unitType"] = "EA",
                ["quantity"] = line.Quantity,
                ["unitPrice"] = line.UnitPrice,
                ["salesTotal"] = line.LineTotal,
                ["total"] = line.LineTotal + lineVat,
                ["valueDifference"] = 0,
                ["totalTaxableFees"] = 0,
                ["netTotal"] = line.LineTotal,
                ["itemsDiscount"] = 0,
                ["taxableItems"] = new JsonArray(new JsonObject
                {
                    ["taxType"] = "T1",
                    ["amount"] = lineVat,
                    ["subType"] = "V009",
                    ["rate"] = settings.VatRate * 100
                })
            });
            lineNumber++;
        }

        return result;
    }

    private static JsonObject BuildDocument(
        string documentType,
        string internalId,
        DateTime dateTimeIssued,
        Customer customer,
        CompanySettings settings,
        decimal subTotal,
        decimal vatAmount,
        decimal totalAmount,
        JsonArray lines,
        string? referencedInternalId)
    {
        var document = new JsonObject
        {
            ["issuer"] = new JsonObject
            {
                ["type"] = "B",
                ["id"] = settings.TaxRegistrationNumber,
                ["name"] = settings.CompanyNameAr
            },
            ["receiver"] = new JsonObject
            {
                ["type"] = string.IsNullOrWhiteSpace(customer.TaxRegistrationNumber) ? "P" : "B",
                ["id"] = customer.TaxRegistrationNumber,
                ["name"] = customer.NameAr
            },
            ["documentType"] = documentType,
            ["documentTypeVersion"] = "1.0",
            ["dateTimeIssued"] = dateTimeIssued.ToString("O"),
            ["internalId"] = internalId,
            ["invoiceLines"] = lines,
            ["totalDiscountAmount"] = 0,
            ["totalSalesAmount"] = subTotal,
            ["netAmount"] = subTotal,
            ["taxTotals"] = new JsonArray(new JsonObject
            {
                ["taxType"] = "T1",
                ["amount"] = vatAmount
            }),
            ["totalAmount"] = totalAmount
        };

        if (referencedInternalId is not null)
        {
            document["references"] = new JsonArray(new JsonObject
            {
                ["internalId"] = referencedInternalId
            });
        }

        return document;
    }
}

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
        var lines = new JsonArray();
        var lineNumber = 1;
        foreach (var line in invoice.Lines)
        {
            var lineVat = Math.Round(line.LineTotal * settings.VatRate, 2);
            lines.Add(new JsonObject
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

        return new JsonObject
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
            ["documentType"] = "I",
            ["documentTypeVersion"] = "1.0",
            ["dateTimeIssued"] = invoice.InvoiceDate.ToString("O"),
            ["internalId"] = invoice.InvoiceNumber,
            ["invoiceLines"] = lines,
            ["totalDiscountAmount"] = 0,
            ["totalSalesAmount"] = invoice.SubTotal,
            ["netAmount"] = invoice.SubTotal,
            ["taxTotals"] = new JsonArray(new JsonObject
            {
                ["taxType"] = "T1",
                ["amount"] = invoice.VatAmount
            }),
            ["totalAmount"] = invoice.TotalAmount
        };
    }
}

using System.Text;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

/// <summary>Builds the ZATCA-mandated invoice QR code: five TLV (Tag-Length-Value) fields — seller name, VAT
/// number, timestamp, invoice total, VAT total — concatenated and base64-encoded. This is the base Phase-1
/// field set that Phase-2 QR codes still carry (Phase 2 additionally embeds the cryptographic stamp and
/// signing certificate as further TLV tags, which requires the real ZATCA-issued certificate and isn't
/// included here — add tags 6-9 once a real certificate is wired in).</summary>
public static class ZatcaQrCodeBuilder
{
    public static string Build(string sellerName, string vatNumber, DateTime timestampUtc, decimal invoiceTotal, decimal vatTotal)
    {
        using var stream = new MemoryStream();
        WriteTlv(stream, 1, sellerName);
        WriteTlv(stream, 2, vatNumber);
        WriteTlv(stream, 3, timestampUtc.ToString("O"));
        WriteTlv(stream, 4, invoiceTotal.ToString("F2"));
        WriteTlv(stream, 5, vatTotal.ToString("F2"));
        return Convert.ToBase64String(stream.ToArray());
    }

    private static void WriteTlv(Stream stream, byte tag, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte(tag);
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }
}

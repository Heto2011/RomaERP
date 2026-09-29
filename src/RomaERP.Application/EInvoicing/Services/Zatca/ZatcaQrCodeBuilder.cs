using System.Text;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

/// <summary>Builds ZATCA's production ("Phase 2") invoice QR code: TLV (Tag-Length-Value — 1-byte tag, 1-byte
/// length, raw value bytes) fields carrying the cryptographic stamp. Tags 1-8 are always present; tag 9
/// (certificate signature) is added only for Simplified (B2C) invoices, per the algorithm this session ported
/// from the openly available "Saleh7/php-zatca-xml" reference implementation (see ZatcaXadesDocumentSigner for
/// the full set of caveats — this has not been validated against ZATCA's own compliance checker).</summary>
public static class ZatcaQrCodeBuilder
{
    public static string Build(
        string sellerName,
        string vatNumber,
        string invoiceTimestampIso,
        decimal invoiceTotal,
        decimal vatTotal,
        string invoiceHashBase64,
        string digitalSignatureBase64,
        byte[] publicKeyDer,
        byte[]? certificateSignature)
    {
        using var stream = new MemoryStream();
        WriteTlv(stream, 1, Encoding.UTF8.GetBytes(sellerName));
        WriteTlv(stream, 2, Encoding.UTF8.GetBytes(vatNumber));
        WriteTlv(stream, 3, Encoding.UTF8.GetBytes(invoiceTimestampIso));
        WriteTlv(stream, 4, Encoding.UTF8.GetBytes(invoiceTotal.ToString("F2")));
        WriteTlv(stream, 5, Encoding.UTF8.GetBytes(vatTotal.ToString("F2")));
        WriteTlv(stream, 6, Encoding.UTF8.GetBytes(invoiceHashBase64));
        WriteTlv(stream, 7, Encoding.UTF8.GetBytes(digitalSignatureBase64));
        WriteTlv(stream, 8, publicKeyDer);
        if (certificateSignature is not null)
            WriteTlv(stream, 9, certificateSignature);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static void WriteTlv(Stream stream, byte tag, byte[] value)
    {
        stream.WriteByte(tag);
        stream.WriteByte(checked((byte)value.Length));
        stream.Write(value, 0, value.Length);
    }
}

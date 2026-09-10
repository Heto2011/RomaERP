namespace RomaERP.Application.Common.Interfaces;

/// <summary>Renders an HTML string to a PDF file's bytes. Used for printable documents (invoices, etc.) —
/// the HTML itself carries all styling, so a document's look is just CSS, not a separate PDF layout API.</summary>
public interface IHtmlToPdfRenderer
{
    Task<byte[]> RenderAsync(string html, CancellationToken ct = default);
}

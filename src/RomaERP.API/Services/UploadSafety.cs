using RomaERP.Application.Common.Exceptions;

namespace RomaERP.API.Services;

/// <summary>Checks for uploaded files: a stored file's extension comes from the client, so it is taken from a fixed
/// list rather than copied, and image/PDF uploads are checked against their real leading bytes.</summary>
public static class UploadSafety
{
    private static readonly string[] AllowedProofExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf" };

    /// <summary>The extension to store a proof (receipt/invoice image or PDF) under — never the client's own text.</summary>
    public static string ProofExtension(string? clientFileName)
    {
        var ext = Path.GetExtension(clientFileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedProofExtensions.Contains(ext))
            throw new ValidationAppException("نوع الملف غير مدعوم — الصور (JPG/PNG/WEBP/GIF) أو PDF فقط.");
        return ext;
    }

    public static bool LooksLikeJpeg(ReadOnlySpan<byte> head) => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;

    public static bool LooksLikePdf(ReadOnlySpan<byte> head) => head.Length >= 5 && head[0] == '%' && head[1] == 'P' && head[2] == 'D' && head[3] == 'F' && head[4] == '-';

    public static async Task<byte[]> ReadHeadAsync(IFormFile file, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(buffer.AsMemory(0, count), ct);
        return buffer[..read];
    }
}

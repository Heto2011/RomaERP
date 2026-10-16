using RomaERP.Application.Common.Exceptions;

namespace RomaERP.Application.Common;

/// <summary>Turns "text longer than the column" into a clear message instead of a database truncation error.</summary>
public static class TextLimit
{
    public static string? Check(string? value, int max, string label)
    {
        var trimmed = value?.Trim();
        if (trimmed is { Length: > 0 } && trimmed.Length > max)
            throw new ValidationAppException($"{label} طويل جدًا (الحد الأقصى {max} حرف).");
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

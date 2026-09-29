using System.Globalization;
using ClosedXML.Excel;
using RomaERP.Application.Common.Exceptions;

namespace RomaERP.Application.Common;

/// <summary>Parses a bank statement CSV without forcing the caller into one rigid column order — banks
/// export wildly different layouts (Date/Description/Amount, separate Debit/Credit columns, Arabic headers,
/// semicolon-delimited files with comma-formatted numbers, DD/MM vs MM/DD dates...). Reads the header row
/// when one is recognized (by matching common English/Arabic column-name synonyms) and maps by column name;
/// falls back to the old positional assumption (first column = date, last column = amount) when the header
/// isn't recognized, so a plain unlabeled 3-column file still works exactly as before.
///
/// Each caller resolves Amount/Debit/Credit into its own signed "amount" per its own convention (this parser
/// stays agnostic to that — different bank-reconciliation flows in this codebase use opposite sign
/// conventions for the same Debit/Credit pair, so baking one in here would silently flip the other's sign).</summary>
public static class BankStatementCsvParser
{
    public record ParsedLine(DateTime Date, string Description, decimal? Amount, decimal? Debit, decimal? Credit);

    // ClosedXML fully materializes the workbook's used range into memory with no built-in cap, so a small
    // but pathologically wide/tall .xlsx (well within the controllers' request-size limit) could still
    // balloon into a huge in-memory cell graph. No real bank statement needs anywhere near this many
    // rows/columns, so reject anything past a generous sanity limit before iterating.
    private const int MaxExcelRows = 50_000;
    private const int MaxExcelColumns = 200;

    private static readonly string[] DateHeaders = { "date", "transaction date", "trans date", "posting date", "value date", "تاريخ", "التاريخ", "تاريخ العملية", "تاريخ الحركة" };
    private static readonly string[] DescriptionHeaders = { "description", "details", "narrative", "memo", "particulars", "reference", "transaction description", "بيان", "البيان", "التفاصيل", "الوصف", "ملاحظات" };
    private static readonly string[] AmountHeaders = { "amount", "value", "transaction amount", "مبلغ", "المبلغ", "القيمة" };
    private static readonly string[] DebitHeaders = { "debit", "debit amount", "withdrawal", "withdrawals", "money out", "مدين", "سحب", "المسحوب" };
    private static readonly string[] CreditHeaders = { "credit", "credit amount", "deposit", "deposits", "money in", "دائن", "إيداع", "المودع" };

    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "yyyy/MM/dd",
        "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"
    };

    /// <summary>Auto-detects the uploaded file's format from its name/content and parses accordingly — a
    /// bank statement downloaded straight from online banking is at least as often a real .xlsx workbook as
    /// a CSV, and making the caller pick the right parser just reintroduces the "wrong format" friction this
    /// class exists to remove.</summary>
    public static async Task<List<ParsedLine>> ParseAsync(Stream stream, string? fileName, CancellationToken ct = default)
    {
        if (IsExcelFile(stream, fileName))
            return ParseExcel(stream);

        return await ParseAsync(stream, ct);
    }

    public static async Task<List<ParsedLine>> ParseAsync(Stream csvStream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var rawLines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                rawLines.Add(line);
        }

        if (rawLines.Count == 0)
            return new List<ParsedLine>();

        var delimiter = DetectDelimiter(rawLines[0]);
        var rows = rawLines.Select(l => SplitRow(l, delimiter)).ToList();
        return ParseRows(rows);
    }

    /// <summary>Reads the first worksheet of an .xlsx/.xls workbook into the same row-processing pipeline
    /// the CSV path uses — cells come back as their underlying value (not Excel's display formatting), so a
    /// date or currency-formatted amount cell converts to a plain, reliably parseable string.</summary>
    private static List<ParsedLine> ParseExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        var range = worksheet?.RangeUsed();
        if (range is null)
            return new List<ParsedLine>();

        if (range.RowCount() > MaxExcelRows || range.ColumnCount() > MaxExcelColumns)
            throw new ValidationAppException("ملف الإكسيل كبير جدًا — الحد الأقصى المسموح به هو 50000 صف و200 عمود.");

        var columnCount = range.ColumnCount();
        var rows = new List<string[]>();
        foreach (var row in range.RowsUsed())
        {
            var fields = row.Cells(1, columnCount).Select(CellToString).ToArray();
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(fields);
        }

        return ParseRows(rows);
    }

    private static string CellToString(IXLCell cell) => CleanField(cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
        _ => cell.GetString(),
    });

    private static bool IsExcelFile(Stream stream, string? fileName)
    {
        var extension = fileName is null ? null : Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".xlsx" or ".xlsm" or ".xls") return true;
        if (extension is ".csv" or ".txt") return false;

        // No (or an unrecognized) extension — fall back to sniffing the .xlsx/.xls "PK" zip-container
        // signature so a mislabeled or extension-less upload still works.
        if (!stream.CanSeek) return false;
        var position = stream.Position;
        Span<byte> header = stackalloc byte[2];
        var bytesRead = stream.Read(header);
        stream.Position = position;
        return bytesRead == 2 && header[0] == 0x50 && header[1] == 0x4B;
    }

    private static List<ParsedLine> ParseRows(List<string[]> rows)
    {
        if (rows.Count == 0)
            return new List<ParsedLine>();

        var columnMap = MapHeaderColumns(rows[0]);
        var dataRows = columnMap is not null ? rows.Skip(1) : rows;

        var result = new List<ParsedLine>();
        foreach (var fields in dataRows)
        {
            var parsed = columnMap is not null
                ? ParseByColumnMap(fields, columnMap)
                : ParsePositional(fields);

            if (parsed is not null)
                result.Add(parsed);
        }

        return result;
    }

    private static char DetectDelimiter(string headerLine)
    {
        var candidates = new[] { ',', ';', '\t' };
        return candidates
            .Select(d => (Delimiter: d, Count: SplitRow(headerLine, d).Length))
            .OrderByDescending(x => x.Count)
            .First().Delimiter;
    }

    /// <summary>Splits on the delimiter but never inside a double-quoted field, then trims surrounding
    /// quotes/whitespace from each resulting field — a minimal quote-aware split, not full RFC 4180.</summary>
    private static string[] SplitRow(string row, char delimiter)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in row)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == delimiter && !inQuotes)
            {
                fields.Add(CleanField(current.ToString()).Trim().Trim('"').Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        fields.Add(CleanField(current.ToString()).Trim().Trim('"').Trim());
        return fields.ToArray();
    }

    /// <summary>Bank exports (especially Arabic ones re-saved from Excel) routinely embed invisible
    /// bidi/BOM marks around values and use Arabic-Indic digits — both make an otherwise well-formed
    /// column silently fail to match a header name or parse as a date/number. Normalize both away for
    /// every field so header recognition and value parsing see plain content.</summary>
    private static string CleanField(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            switch (ch)
            {
                case '\uFEFF': // BOM
                case '\u200B': // zero-width space
                case '\u200E': // left-to-right mark
                case '\u200F': // right-to-left mark
                    continue;
                case '\u00A0': // non-breaking space
                    sb.Append(' ');
                    continue;
            }

            if (ch is >= '\u0660' and <= '\u0669') sb.Append((char)('0' + (ch - '\u0660'))); // Arabic-Indic digits
            else if (ch is >= '\u06F0' and <= '\u06F9') sb.Append((char)('0' + (ch - '\u06F0'))); // Extended Arabic-Indic digits
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private record ColumnMap(int DateIndex, int? DescriptionIndex, int? AmountIndex, int? DebitIndex, int? CreditIndex);

    private static ColumnMap? MapHeaderColumns(string[] headerFields)
    {
        int? dateIdx = null, descIdx = null, amountIdx = null, debitIdx = null, creditIdx = null;

        for (var i = 0; i < headerFields.Length; i++)
        {
            var normalized = headerFields[i].Trim().ToLowerInvariant();
            if (dateIdx is null && DateHeaders.Contains(normalized)) dateIdx = i;
            else if (descIdx is null && DescriptionHeaders.Contains(normalized)) descIdx = i;
            else if (amountIdx is null && AmountHeaders.Contains(normalized)) amountIdx = i;
            else if (debitIdx is null && DebitHeaders.Contains(normalized)) debitIdx = i;
            else if (creditIdx is null && CreditHeaders.Contains(normalized)) creditIdx = i;
        }

        // Only trust the header if it gives us a date column AND some way to get an amount —
        // otherwise fall back to the positional assumption instead of guessing.
        if (dateIdx is null || (amountIdx is null && debitIdx is null && creditIdx is null))
            return null;

        return new ColumnMap(dateIdx.Value, descIdx, amountIdx, debitIdx, creditIdx);
    }

    private static ParsedLine? ParseByColumnMap(string[] fields, ColumnMap map)
    {
        if (map.DateIndex >= fields.Length || !TryParseDate(fields[map.DateIndex], out var date))
            return null;

        decimal? amount = map.AmountIndex is { } ai && ai < fields.Length && TryParseAmount(fields[ai], out var a) ? a : null;
        decimal? debit = map.DebitIndex is { } di && di < fields.Length && TryParseAmount(fields[di], out var d) ? d : null;
        decimal? credit = map.CreditIndex is { } ci && ci < fields.Length && TryParseAmount(fields[ci], out var c) ? c : null;

        if (amount is null && debit is null && credit is null)
            return null;

        var description = map.DescriptionIndex is { } desci && desci < fields.Length ? fields[desci] : string.Empty;
        return new ParsedLine(date, description, amount, debit, credit);
    }

    /// <summary>The original, header-agnostic fallback: first column is the date, last column is the
    /// amount, everything in between (rejoined with commas) is the description.</summary>
    private static ParsedLine? ParsePositional(string[] fields)
    {
        if (fields.Length < 3)
            return null;

        if (!TryParseDate(fields[0], out var date))
            return null;

        if (!TryParseAmount(fields[^1], out var amount))
            return null;

        var description = string.Join(", ", fields.Skip(1).Take(fields.Length - 2));
        return new ParsedLine(date, description, amount, null, null);
    }

    private static bool TryParseDate(string raw, out DateTime date)
    {
        raw = raw.Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            amount = 0;
            return false;
        }

        // Some banks show a withdrawal as "(123.45)" instead of "-123.45".
        var negativeParens = raw.StartsWith('(') && raw.EndsWith(')');
        if (negativeParens)
            raw = raw[1..^1];

        // Some banks inline a currency code/symbol with the amount (e.g. "SAR 1,500.00", "1500 ر.س").
        // Strip anything that isn't part of the number itself rather than requiring the caller's file
        // to be bare — NumberStyles' currency support only recognizes the invariant "¤" symbol.
        raw = new string(raw.Where(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+').ToArray());

        if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
            return false;

        if (negativeParens)
            amount = -amount;

        return true;
    }
}

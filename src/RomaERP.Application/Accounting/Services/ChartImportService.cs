using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

/// <summary>Lets a company replace its chart of accounts from an Excel/CSV file. The file is only read into an editable list first; nothing is
/// saved until the customer approves. History is never rewritten: an old account that has entries (or that anything else points to) is kept and
/// switched off, an account the system itself posts to is kept as it is, and only an account nothing uses is removed.</summary>
public class ChartImportService : IChartImportService
{
    private const int MaxRows = 5000;
    private static readonly Regex ArabicLetters = new(@"\p{IsArabic}", RegexOptions.Compiled);

    /// <summary>Codes the application posts to by itself (cash, customers, sales, payroll...), read from the constants it uses.</summary>
    private static readonly HashSet<string> SystemCodes = typeof(AccountingConstants)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("AccountCode", StringComparison.Ordinal))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    private readonly IApplicationDbContext _context;

    public ChartImportService(IApplicationDbContext context) => _context = context;

    // ---------------------------------------------------------------- reading the file

    public async Task<ChartImportPreviewDto> PreviewFileAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        var notes = new List<string>();
        List<ChartImportRowDto> rows;
        var lower = (fileName ?? string.Empty).ToLowerInvariant();
        if (lower.EndsWith(".xlsx") || lower.EndsWith(".xlsm"))
            rows = ReadExcel(file, notes);
        else if (lower.EndsWith(".csv") || lower.EndsWith(".txt") || lower.EndsWith(".tsv"))
            rows = ReadCsv(file, notes);
        else if (lower.EndsWith(".pdf"))
            throw new ValidationAppException("رفع الشجرة من PDF لسه مش متاح. حوّل الملف لـ Excel أو CSV، أو نزّل النموذج وعبّيه.");
        else
            throw new ValidationAppException("نوع الملف غير مدعوم. ارفع ملف Excel (.xlsx) أو CSV.");

        if (rows.Count == 0) throw new ValidationAppException("ما لقيتش أي حسابات في الملف. تأكد إن فيه عمود لكود الحساب وعمود للاسم.");
        if (rows.Count > MaxRows) throw new ValidationAppException($"الملف فيه أكتر من {MaxRows} حساب.");

        var preview = await BuildPreviewAsync(rows, ct);
        preview.FileNotes.InsertRange(0, notes);
        return preview;
    }

    private static List<ChartImportRowDto> ReadExcel(Stream file, List<string> notes)
    {
        using var workbook = new XLWorkbook(file);
        var sheet = workbook.Worksheets.FirstOrDefault(w => w.RangeUsed() is not null)
            ?? throw new ValidationAppException("الملف فاضي.");
        var used = sheet.RangeUsed()!;
        var table = new List<string[]>();
        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();
        for (var r = firstRow; r <= lastRow; r++)
        {
            var cells = new string[lastCol - firstCol + 1];
            for (var c = firstCol; c <= lastCol; c++) cells[c - firstCol] = sheet.Cell(r, c).GetFormattedString().Trim();
            table.Add(cells);
        }
        if (workbook.Worksheets.Count > 1) notes.Add($"الملف فيه أكتر من ورقة، اتقرأت الورقة \"{sheet.Name}\" بس.");
        return ParseTable(table, notes);
    }

    private static List<ChartImportRowDto> ReadCsv(Stream file, List<string> notes)
    {
        using var reader = new StreamReader(file, Encoding.UTF8, true);
        var text = reader.ReadToEnd();
        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) throw new ValidationAppException("الملف فاضي.");
        var first = lines[0];
        var delimiter = first.Count(ch => ch == '\t') > 0 ? '\t' : first.Count(ch => ch == ';') > first.Count(ch => ch == ',') ? ';' : ',';
        var table = lines.Select(l => SplitCsvLine(l, delimiter)).ToList();
        return ParseTable(table, notes);
    }

    private static string[] SplitCsvLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else quoted = !quoted; }
            else if (ch == delimiter && !quoted) { cells.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(ch);
        }
        cells.Add(sb.ToString().Trim());
        return cells.ToArray();
    }

    private enum Col { Code, NameAr, NameEn, Name, Type, Nature, Parent }

    private static readonly (Col Col, string[] Words)[] HeaderWords =
    {
        (Col.Code, new[] { "code", "account code", "account no", "account number", "no", "كود", "كود الحساب", "رقم الحساب", "رقم", "الكود" }),
        (Col.NameAr, new[] { "name ar", "arabic", "arabic name", "name (ar)", "الاسم بالعربي", "الاسم العربي", "اسم عربي", "اسم الحساب بالعربي", "الاسم عربي" }),
        (Col.NameEn, new[] { "name en", "english", "english name", "name (en)", "الاسم بالانجليزي", "الاسم الانجليزي", "الاسم بالإنجليزي", "اسم انجليزي", "اسم إنجليزي", "الاسم انجليزي" }),
        (Col.Name, new[] { "name", "account name", "account", "title", "الاسم", "اسم الحساب", "الحساب", "البيان" }),
        (Col.Type, new[] { "type", "account type", "category", "class", "النوع", "نوع الحساب", "التصنيف", "الفئة" }),
        (Col.Nature, new[] { "nature", "normal balance", "balance", "الطبيعة", "طبيعة الحساب", "رصيد" }),
        (Col.Parent, new[] { "parent", "parent code", "parent account", "الحساب الأب", "الأب", "الحساب الرئيسي", "الرئيسي", "كود الأب", "كود الحساب الرئيسي" }),
    };

    private static string Norm(string s) => Regex.Replace((s ?? string.Empty).Trim().ToLowerInvariant().Replace("أ", "ا").Replace("إ", "ا").Replace("ة", "ه").Replace("ى", "ي"), @"\s+", " ");

    private static List<ChartImportRowDto> ParseTable(List<string[]> table, List<string> notes)
    {
        // Find the header: the first of the top rows that has a recognisable "code" column.
        var map = new Dictionary<Col, int>();
        var headerAt = -1;
        for (var r = 0; r < Math.Min(table.Count, 10) && headerAt < 0; r++)
        {
            var found = new Dictionary<Col, int>();
            for (var c = 0; c < table[r].Length; c++)
            {
                var cell = Norm(table[r][c]);
                if (cell.Length == 0) continue;
                foreach (var (col, words) in HeaderWords)
                    if (!found.ContainsKey(col) && words.Any(w => Norm(w) == cell)) { found[col] = c; break; }
            }
            if (found.ContainsKey(Col.Code) && (found.ContainsKey(Col.Name) || found.ContainsKey(Col.NameAr) || found.ContainsKey(Col.NameEn)))
            {
                map = found; headerAt = r;
            }
        }
        if (headerAt < 0)
        {
            // No header: first column is the code, second the name, optional third the type.
            map = new Dictionary<Col, int> { [Col.Code] = 0, [Col.Name] = 1 };
            if (table.Any() && table[0].Length > 2) map[Col.Type] = 2;
            notes.Add("ما لقيتش صف عناوين، فاعتبرت العمود الأول كود الحساب والتاني الاسم.");
        }

        string Get(string[] row, Col col) => map.TryGetValue(col, out var i) && i < row.Length ? row[i].Trim() : string.Empty;

        var rows = new List<ChartImportRowDto>();
        for (var r = headerAt + 1; r < table.Count; r++)
        {
            var cells = table[r];
            var code = Regex.Replace(Get(cells, Col.Code), @"\s+", "");
            if (code.EndsWith(".0")) code = code[..^2];     // Excel turns 1000 into "1000.0" when a column is numeric
            var nameAr = Get(cells, Col.NameAr);
            var nameEn = Get(cells, Col.NameEn);
            var name = Get(cells, Col.Name);
            if (code.Length == 0 && name.Length == 0 && nameAr.Length == 0 && nameEn.Length == 0) continue;

            if (name.Length > 0 && nameAr.Length == 0 && nameEn.Length == 0)
            {
                if (ArabicLetters.IsMatch(name)) nameAr = name; else nameEn = name;
            }
            var row = new ChartImportRowDto { Code = code, NameAr = nameAr, NameEn = nameEn, ParentCode = Get(cells, Col.Parent) is { Length: > 0 } p ? CleanCode(p) : null };

            var typeText = Get(cells, Col.Type);
            if (typeText.Length > 0)
            {
                if (TryParseType(typeText, out var type)) row.AccountType = type;
                else row.Errors.Add($"نوع الحساب \"{typeText}\" مش معروف (أصول، خصوم، حقوق ملكية، إيرادات، مصروفات).");
            }
            else if (TypeFromCode(code) is { } inferred) { row.AccountType = inferred; row.Warnings.Add("نوع الحساب اتحدد من أول رقم في الكود."); }
            else row.Errors.Add("اكتب نوع الحساب.");

            var natureText = Get(cells, Col.Nature);
            if (natureText.Length > 0 && TryParseNature(natureText, out var nature)) row.Nature = nature;
            else row.Nature = DefaultNature(row.AccountType);
            rows.Add(row);
        }
        return rows;
    }

    private static string CleanCode(string s)
    {
        var c = Regex.Replace(s ?? string.Empty, @"\s+", "");
        return c.EndsWith(".0") ? c[..^2] : c;
    }

    private static AccountNature DefaultNature(AccountType type) => type is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit;

    private static AccountType? TypeFromCode(string code)
        => code.Length > 0 && code[0] is >= '1' and <= '5' ? (AccountType)(code[0] - '0') : null;

    private static bool TryParseType(string text, out AccountType type)
    {
        var t = Norm(text);
        type = default;
        if (int.TryParse(t, out var n) && n is >= 1 and <= 5) { type = (AccountType)n; return true; }
        if (t.StartsWith("asset") || t.StartsWith("اصول") || t.StartsWith("اصل")) { type = AccountType.Asset; return true; }
        if (t.StartsWith("liab") || t.StartsWith("خصوم") || t.StartsWith("التزام") || t.StartsWith("خصم")) { type = AccountType.Liability; return true; }
        if (t.StartsWith("equity") || t.StartsWith("capital") || t.StartsWith("حقوق") || t.StartsWith("راس")) { type = AccountType.Equity; return true; }
        if (t.StartsWith("rev") || t.StartsWith("income") || t.StartsWith("sales") || t.StartsWith("ايراد") || t.StartsWith("دخل") || t.StartsWith("مبيعات")) { type = AccountType.Revenue; return true; }
        if (t.StartsWith("exp") || t.StartsWith("cost") || t.StartsWith("مصروف") || t.StartsWith("مصاريف") || t.StartsWith("تكلف")) { type = AccountType.Expense; return true; }
        return false;
    }

    private static bool TryParseNature(string text, out AccountNature nature)
    {
        var t = Norm(text);
        nature = default;
        if (t.StartsWith("debit") || t.StartsWith("dr") || t.StartsWith("مدين")) { nature = AccountNature.Debit; return true; }
        if (t.StartsWith("credit") || t.StartsWith("cr") || t.StartsWith("دائن")) { nature = AccountNature.Credit; return true; }
        return false;
    }

    // ---------------------------------------------------------------- checking

    public Task<ChartImportPreviewDto> PreviewRowsAsync(List<ChartImportRowDto> rows, CancellationToken ct = default)
    {
        if (rows is null || rows.Count == 0) throw new ValidationAppException("مفيش حسابات للمعاينة.");
        if (rows.Count > MaxRows) throw new ValidationAppException($"أكتر من {MaxRows} حساب.");
        return BuildPreviewAsync(rows, ct);
    }

    private sealed class Plan
    {
        public List<Account> Remove { get; } = new();
        public List<Account> Deactivate { get; } = new();
        public List<string> KeptForSystem { get; } = new();
        public int Created { get; set; }
        public int Updated { get; set; }
        public List<string> Notes { get; } = new();
    }

    private async Task<ChartImportPreviewDto> BuildPreviewAsync(List<ChartImportRowDto> rows, CancellationToken ct)
    {
        Validate(rows);
        var existing = await _context.Accounts.AsNoTracking().ToListAsync(ct);      // the query filter keeps removed accounts out
        var (used, complete) = await _context.GetAccountIdsInUseAsync(ct);
        var plan = MakePlan(rows, existing, used, complete);

        var errors = rows.Sum(r => r.Errors.Count);
        return new ChartImportPreviewDto
        {
            Rows = rows,
            ErrorCount = errors,
            WarningCount = rows.Sum(r => r.Warnings.Count),
            CanApply = errors == 0 && rows.Count > 0,
            Summary = new ChartImportSummaryDto
            {
                NewAccounts = plan.Created, UpdatedAccounts = plan.Updated, RemovedAccounts = plan.Remove.Count,
                DeactivatedAccounts = plan.Deactivate.Count, KeptForSystem = plan.KeptForSystem, Notes = plan.Notes,
            },
        };
    }

    /// <summary>Checks every line and writes what is wrong (Errors) or doubtful (Warnings) onto it. Also fills in what can be worked out:
    /// a missing parent from the code pattern, and which accounts are headers.</summary>
    private static void Validate(List<ChartImportRowDto> rows)
    {
        foreach (var r in rows)
        {
            r.Errors.RemoveAll(e => !e.StartsWith("نوع الحساب") && e != "اكتب نوع الحساب.");      // keep the file's own type errors until the type is fixed
            r.Warnings.Clear();
            r.Code = CleanCode(r.Code);
            r.ParentCode = string.IsNullOrWhiteSpace(r.ParentCode) ? null : CleanCode(r.ParentCode!);
            r.NameAr = (r.NameAr ?? string.Empty).Trim();
            r.NameEn = (r.NameEn ?? string.Empty).Trim();
            if (Enum.IsDefined(r.AccountType)) r.Errors.RemoveAll(e => e.StartsWith("نوع الحساب") || e == "اكتب نوع الحساب.");
            else r.Errors.Add("نوع الحساب غير صحيح.");
        }

        var byCode = new Dictionary<string, ChartImportRowDto>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (r.Code.Length == 0) { r.Errors.Add("كود الحساب مطلوب."); continue; }
            if (r.Code.Length > 20) { r.Errors.Add("كود الحساب أطول من 20 حرف."); continue; }
            if (!byCode.TryAdd(r.Code, r)) r.Errors.Add($"الكود {r.Code} مكرر في الملف.");
        }

        foreach (var r in rows)
        {
            if (r.NameAr.Length == 0 && r.NameEn.Length == 0) r.Errors.Add("اسم الحساب مطلوب.");
            else if (r.NameAr.Length == 0) { r.NameAr = r.NameEn; r.Warnings.Add("الاسم العربي فاضي، اتكرر الاسم الإنجليزي."); }
            else if (r.NameEn.Length == 0) { r.NameEn = r.NameAr; r.Warnings.Add("الاسم الإنجليزي فاضي، اتكرر الاسم العربي."); }
            if (r.NameAr.Length > 200 || r.NameEn.Length > 200) r.Errors.Add("الاسم أطول من 200 حرف.");
        }

        // A parent that was not written is worked out from the code (1111 -> 1110 -> 1100 -> 1000), when that account is in the file.
        foreach (var r in rows.Where(r => r.ParentCode is null && r.Code.Length > 1 && r.Code.All(char.IsDigit) && byCode.ContainsKey(r.Code)))
        {
            for (var i = r.Code.Length - 1; i >= 1; i--)
            {
                var candidate = r.Code[..i] + new string('0', r.Code.Length - i);
                if (candidate != r.Code && byCode.ContainsKey(candidate)) { r.ParentCode = candidate; r.Warnings.Add($"الحساب الأب اتحدد تلقائي: {candidate}."); break; }
            }
        }

        foreach (var r in rows.Where(r => r.ParentCode is not null))
        {
            if (r.ParentCode == r.Code) { r.Errors.Add("الحساب مينفعش يبقى أب نفسه."); continue; }
            if (!byCode.TryGetValue(r.ParentCode!, out var parent)) { r.Errors.Add($"الحساب الأب {r.ParentCode} مش موجود في الملف."); continue; }
            if (parent.AccountType != r.AccountType) r.Errors.Add($"الحساب الفرعي لازم يبقى من نفس نوع الحساب الأب ({r.ParentCode}).");
        }

        // A parent chain that loops back on itself.
        foreach (var r in rows.Where(r => r.Errors.Count == 0 && r.ParentCode is not null))
        {
            var seen = new HashSet<string> { r.Code };
            var cur = r;
            while (cur.ParentCode is not null && byCode.TryGetValue(cur.ParentCode, out var up))
            {
                if (!seen.Add(up.Code)) { r.Errors.Add("الحساب الأب بيعمل دايرة (حساب أب لنفسه من بعيد)."); break; }
                cur = up;
            }
        }

        var parentCodes = rows.Where(r => r.ParentCode is not null).Select(r => r.ParentCode!).ToHashSet(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            r.IsControlAccount = parentCodes.Contains(r.Code);
            if (SystemCodes.Contains(r.Code))
            {
                var expected = SystemExpectation(r.Code);
                if (expected is { } t && t != r.AccountType) r.Warnings.Add($"النظام بيستخدم الحساب {r.Code} كحساب من نوع آخر؛ راجع النوع.");
            }
        }
    }

    private static AccountType? SystemExpectation(string code) => TypeFromCode(code);

    private static Plan MakePlan(List<ChartImportRowDto> rows, List<Account> existing, HashSet<Guid> used, bool complete)
    {
        var plan = new Plan();
        var fileCodes = rows.Where(r => r.Code.Length > 0).Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var byCode = existing.ToDictionary(a => a.Code, StringComparer.Ordinal);

        foreach (var r in rows.Where(r => r.Code.Length > 0))
        {
            if (byCode.TryGetValue(r.Code, out var old))
            {
                plan.Updated++;
                var inUse = !complete || used.Contains(old.Id);
                if (inUse && (old.AccountType != r.AccountType || old.Nature != r.Nature))
                {
                    r.Warnings.Add("الحساب عليه حركات، فنوعه وطبيعته القديمة هتفضل زي ما هي.");
                }
            }
            else plan.Created++;
        }

        var missing = existing.Where(a => !fileCodes.Contains(a.Code)).ToList();
        var keep = new HashSet<Guid>();
        foreach (var a in missing)
        {
            if (SystemCodes.Contains(a.Code)) { keep.Add(a.Id); plan.KeptForSystem.Add($"{a.Code} - {a.NameAr}"); }
            else if (!complete || used.Contains(a.Id)) keep.Add(a.Id);
        }
        // An account that stays needs its parents to stay too, or the tree breaks.
        var byId = existing.ToDictionary(a => a.Id);
        foreach (var id in keep.ToList())
        {
            var cur = byId[id];
            while (cur.ParentAccountId is { } pid && byId.TryGetValue(pid, out var parent) && !fileCodes.Contains(parent.Code))
            {
                if (!keep.Add(parent.Id)) break;
                cur = parent;
            }
        }
        foreach (var a in missing)
        {
            if (!keep.Contains(a.Id)) plan.Remove.Add(a);
            else if (!SystemCodes.Contains(a.Code)) plan.Deactivate.Add(a);
        }
        if (!complete) plan.Notes.Add("ما قدرناش نتأكد من ارتباطات الحسابات القديمة، فهتتوقف بدل ما تتمسح.");
        if (plan.Deactivate.Count > 0) plan.Notes.Add("الحسابات القديمة اللي عليها قيود أو مرتبطة بحاجة هتفضل موجودة ومتوقفة، والقيود والتقارير القديمة مش هتتأثر.");
        return plan;
    }

    // ---------------------------------------------------------------- approving

    public async Task<ChartImportResultDto> ApplyAsync(List<ChartImportRowDto> rows, CancellationToken ct = default)
    {
        if (rows is null || rows.Count == 0) throw new ValidationAppException("مفيش حسابات للاعتماد.");
        if (rows.Count > MaxRows) throw new ValidationAppException($"أكتر من {MaxRows} حساب.");
        Validate(rows);
        if (rows.Any(r => r.Errors.Count > 0))
            throw new ValidationAppException("في أخطاء في الحسابات لازم تتصلّح قبل الاعتماد.");

        var all = await _context.Accounts.IgnoreQueryFilters().ToListAsync(ct);          // removed ones too: their codes are still taken
        var live = all.Where(a => !a.IsDeleted).ToList();
        var (used, complete) = await _context.GetAccountIdsInUseAsync(ct);
        var plan = MakePlan(rows, live, used, complete);

        var byCode = all.ToDictionary(a => a.Code, StringComparer.Ordinal);
        var fileAccounts = new Dictionary<string, Account>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            byCode.TryGetValue(r.Code, out var account);
            var isNew = account is null;
            if (account is null)
            {
                account = new Account { Code = r.Code };
                _context.Accounts.Add(account);
            }
            else if (account.IsDeleted)
            {
                account.IsDeleted = false;      // the same code was removed before: bring it back instead of colliding with it
            }
            var inUse = !isNew && (!complete || used.Contains(account.Id));
            account.NameAr = r.NameAr;
            account.NameEn = r.NameEn;
            account.IsControlAccount = r.IsControlAccount;
            account.IsActive = true;
            if (!inUse) { account.AccountType = r.AccountType; account.Nature = r.Nature; }
            fileAccounts[r.Code] = account;
        }
        // parents (needs every account to exist first)
        foreach (var r in rows)
        {
            var account = fileAccounts[r.Code];
            account.ParentAccountId = null;
            account.ParentAccount = r.ParentCode is null ? null : fileAccounts[r.ParentCode];
        }

        foreach (var old in plan.Remove) { old.IsDeleted = true; old.IsActive = false; }
        foreach (var old in plan.Deactivate) old.IsActive = false;

        // levels
        var levelOf = new Dictionary<Account, int>();
        int Level(Account a)
        {
            if (levelOf.TryGetValue(a, out var known)) return known;
            levelOf[a] = 1;       // guards against a loop
            var parent = a.ParentAccount ?? (a.ParentAccountId is { } pid ? all.FirstOrDefault(x => x.Id == pid) : null);
            return levelOf[a] = parent is null ? 1 : Level(parent) + 1;
        }
        foreach (var a in all.Concat(fileAccounts.Values).Distinct().Where(a => !a.IsDeleted)) a.Level = Level(a);

        await _context.SaveChangesAsync(ct);

        return new ChartImportResultDto
        {
            Summary = new ChartImportSummaryDto
            {
                NewAccounts = plan.Created, UpdatedAccounts = plan.Updated, RemovedAccounts = plan.Remove.Count,
                DeactivatedAccounts = plan.Deactivate.Count, KeptForSystem = plan.KeptForSystem, Notes = plan.Notes,
            },
        };
    }

    // ---------------------------------------------------------------- template

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Chart of accounts");
        var headers = new[] { "Code", "Name (Arabic)", "Name (English)", "Type", "Parent code", "Nature" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }
        var sample = new[]
        {
            new[] { "1000", "الأصول", "Assets", "Asset", "", "Debit" },
            new[] { "1100", "الأصول المتداولة", "Current Assets", "Asset", "1000", "Debit" },
            new[] { "1111", "الصندوق", "Cash on Hand", "Asset", "1100", "Debit" },
            new[] { "2000", "الخصوم", "Liabilities", "Liability", "", "Credit" },
            new[] { "4100", "إيرادات المبيعات", "Sales Revenue", "Revenue", "", "Credit" },
            new[] { "5300", "مصروفات عمومية", "General Expenses", "Expense", "", "Debit" },
        };
        for (var r = 0; r < sample.Length; r++)
            for (var c = 0; c < sample[r].Length; c++)
                sheet.Cell(r + 2, c + 1).SetValue(sample[r][c]);
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Restaurant.Services;

public interface IMenuImportService
{
    Task<MenuImportResultDto> ImportAsync(MenuImportRequestDto request, CancellationToken ct = default);
}

/// <summary>Loads a customer's existing menu in one go: categories are created as needed, each dish becomes a menu item
/// that is sold without stock tracking (stock/recipes can be attached later from the menu screen). A preview run
/// (Apply = false) reports exactly what would happen without saving anything.</summary>
public class MenuImportService : IMenuImportService
{
    public const int MaxRows = 1000;
    private const string DefaultUnit = "طبق";

    private readonly IApplicationDbContext _context;

    public MenuImportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MenuImportResultDto> ImportAsync(MenuImportRequestDto request, CancellationToken ct = default)
    {
        if (request.Rows.Count == 0) throw new ValidationAppException("مفيش أصناف في الملف.");
        if (request.Rows.Count > MaxRows) throw new ValidationAppException($"الحد الأقصى {MaxRows} صنف في المرة الواحدة.");

        var categories = await _context.ItemCategories.ToListAsync(ct);
        var categoryByName = categories.GroupBy(c => Key(c.NameAr)).ToDictionary(g => g.Key, g => g.First());
        var items = await _context.Items.ToListAsync(ct);
        var itemByName = items.GroupBy(i => Key(i.NameAr)).ToDictionary(g => g.Key, g => g.First());

        var nextCategoryNo = NextNumber(categories.Select(c => c.Code), "MC-");
        var nextItemNo = NextNumber(items.Select(i => i.Code), "M-");

        var result = new MenuImportResultDto { Applied = request.Apply };
        var seenInFile = new HashSet<string>();
        var rowNo = 0;

        foreach (var row in request.Rows)
        {
            rowNo++;
            var line = new MenuImportLineResultDto { Row = rowNo, Category = (row.Category ?? "").Trim(), NameAr = (row.NameAr ?? "").Trim(), Price = row.Price };
            result.Lines.Add(line);

            var error = Validate(line, row);
            if (error is null && !seenInFile.Add(Key(line.NameAr))) error = "الصنف مكرر في الملف.";
            if (error is not null) { Fail(result, line, error); continue; }

            if (itemByName.TryGetValue(Key(line.NameAr), out var existing))
            {
                if (!existing.IsMenuItem) { Fail(result, line, "في صنف مخزون بنفس الاسم — غيّر الاسم أو ضيفه للمنيو من شاشة المنيو."); continue; }
                if (existing.MenuPrice == line.Price) { line.Action = "same"; result.Unchanged++; continue; }
                if (!request.UpdateExistingPrices) { line.Action = "same"; line.Message = $"موجود بسعر {existing.MenuPrice:0.##} (السعر مش هيتغير)."; result.Unchanged++; continue; }
                line.Action = "update"; line.Message = $"السعر من {existing.MenuPrice:0.##} إلى {line.Price:0.##}"; result.Updated++;
                if (request.Apply) existing.MenuPrice = line.Price;
                continue;
            }

            if (!categoryByName.TryGetValue(Key(line.Category), out var category))
            {
                category = new ItemCategory { Code = $"MC-{nextCategoryNo++:D4}", NameAr = line.Category, NameEn = line.Category };
                categoryByName[Key(line.Category)] = category;
                result.NewCategories++;
                if (request.Apply) _context.ItemCategories.Add(category);
            }

            line.Action = "create"; result.Created++;
            if (request.Apply)
            {
                var item = new Item
                {
                    Code = $"M-{nextItemNo:D5}", NameAr = line.NameAr, NameEn = string.IsNullOrWhiteSpace(row.NameEn) ? line.NameAr : row.NameEn.Trim(),
                    UnitOfMeasure = DefaultUnit, ItemCategory = category, IsMenuItem = true, MenuPrice = line.Price, SoldWithoutStock = true
                };
                _context.Items.Add(item);
                itemByName[Key(line.NameAr)] = item;
            }
            nextItemNo++;
        }

        if (request.Apply && result.Errors > 0)
            throw new ValidationAppException($"في {result.Errors} سطر فيه أخطاء — صلّحهم الأول وجرّب تاني (مفيش حاجة اتحفظت).");

        if (request.Apply) await _context.SaveChangesAsync(ct);
        return result;
    }

    private static string? Validate(MenuImportLineResultDto line, MenuImportRowDto row)
    {
        if (line.NameAr.Length == 0) return "اسم الصنف فاضي.";
        if (line.NameAr.Length > 200) return "اسم الصنف طويل جدًا (أقصى حد 200 حرف).";
        if (line.Category.Length == 0) return "القسم فاضي.";
        if (line.Category.Length > 200) return "اسم القسم طويل جدًا (أقصى حد 200 حرف).";
        if (row.NameEn is { Length: > 200 }) return "الاسم بالإنجليزي طويل جدًا.";
        if (row.Price < 0 || row.Price > 1_000_000) return "السعر غير صحيح.";
        return null;
    }

    private static void Fail(MenuImportResultDto result, MenuImportLineResultDto line, string message)
    {
        line.Action = "error"; line.Message = message; result.Errors++;
    }

    private static string Key(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static int NextNumber(IEnumerable<string> codes, string prefix)
    {
        var max = 0;
        foreach (var code in codes)
            if (code.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(code.AsSpan(prefix.Length), out var n) && n > max) max = n;
        return max + 1;
    }
}

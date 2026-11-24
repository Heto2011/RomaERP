using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Common;

namespace RomaERP.Application.Assistant.Services;

public class AssistantActionService : IAssistantActionService
{
    public const int MaxCandidates = 5;
    public const int MaxLines = 30;

    private readonly IClaudeActionParser _parser;
    private readonly IApplicationDbContext _context;
    private readonly ISalesService _sales;

    public AssistantActionService(IClaudeActionParser parser, IApplicationDbContext context, ISalesService sales)
    {
        _parser = parser;
        _context = context;
        _sales = sales;
    }

    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public async Task<ActionProposalDto> ProposeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ValidationAppException("اكتب الطلب الأول.");
        if (text.Length > 1000) throw new ValidationAppException("الطلب طويل. اكتبه في جملة أو جملتين.");

        var intent = await _parser.ParseAsync(text.Trim(), ct);
        return await ProposeFromIntentAsync(intent, ct);
    }

    /// <summary>Matches names in an already-understood request to real records and returns the draft. Used by the one-sentence page and by the assistant's tools.</summary>
    public async Task<ActionProposalDto> ProposeFromIntentAsync(ActionIntent intent, CancellationToken ct = default)
    {
        var p = new ActionProposalDto { Kind = intent.Kind, Question = intent.Question };
        switch (intent.Kind)
        {
            case AssistantActionKind.SalesInvoice: await FillInvoiceAsync(p, intent, ct); break;
            case AssistantActionKind.CustomerPayment: await FillPaymentAsync(p, intent, ct); break;
            case AssistantActionKind.MenuPrice: await FillMenuPriceAsync(p, intent, ct); break;
            default:
                p.Kind = AssistantActionKind.None;
                p.Summary = intent.Question ?? "ما فهمتش طلب أقدر أجهّزه. جرّب: \"اعمل فاتورة لأحمد بـ 3 قهوة سعر الواحدة 40\".";
                break;
        }
        return p;
    }

    private async Task FillInvoiceAsync(ActionProposalDto p, ActionIntent intent, CancellationToken ct)
    {
        p.Note = intent.Note;
        p.PaymentTerm = intent.PaymentTerm switch { "cash" => 1, "card" => 2, _ => 3 };
        p.Lines = intent.Lines.Take(MaxLines).Select(l => new ActionInvoiceLineDto { Description = l.Description, Quantity = l.Quantity <= 0 ? 1 : l.Quantity, UnitPrice = l.UnitPrice }).ToList();

        var query = Norm(intent.CustomerName);
        var customers = await _context.Customers.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive).ToListAsync(ct);
        var matches = query.Length == 0 ? new() : customers.Where(c => Norm(c.NameAr).Contains(query) || Norm(c.NameEn).Contains(query)).ToList();
        var exact = matches.Where(c => Norm(c.NameAr) == query || Norm(c.NameEn) == query).ToList();
        if (exact.Count == 1) matches = exact;
        p.CustomerCandidates = matches.Take(MaxCandidates).Select(c => new ActionCandidateDto(c.Id, c.NameAr, c.Phone ?? c.Mobile)).ToList();
        if (matches.Count == 1) { p.CustomerId = matches[0].Id; p.CustomerName = matches[0].NameAr; }
        else if (matches.Count == 0) p.Warnings.Add(query.Length == 0 ? "ما ذكرتش اسم العميل." : $"مفيش عميل اسمه \"{intent.CustomerName}\". ضيفه الأول من صفحة العملاء.");
        else p.Warnings.Add("في أكتر من عميل بنفس الاسم. اختار العميل الصح.");

        // A price the owner did not say may come from the menu, if the line names a menu item; otherwise the owner types it.
        var needsPrice = p.Lines.Where(l => l.UnitPrice is null).ToList();
        if (needsPrice.Count > 0)
        {
            var menu = await _context.Items.AsNoTracking().Where(i => !i.IsDeleted && i.IsMenuItem && !i.IsOpenPrice).ToListAsync(ct);
            foreach (var line in needsPrice)
            {
                var d = Norm(line.Description);
                var hit = menu.Where(i => d.Length > 0 && (Norm(i.NameAr) == d || Norm(i.NameEn) == d)).ToList();
                if (hit.Count == 1) line.UnitPrice = hit[0].MenuPrice;
            }
        }
        if (p.Lines.Count == 0) p.Warnings.Add("ما ذكرتش أصناف أو خدمات للفاتورة.");
        if (p.Lines.Any(l => l.UnitPrice is null)) p.Warnings.Add("في بنود من غير سعر. اكتب السعر قبل التنفيذ.");
        p.Summary = $"فاتورة مبيعات{(p.CustomerName is null ? string.Empty : $" للعميل {p.CustomerName}")}.";
    }

    private async Task FillPaymentAsync(ActionProposalDto p, ActionIntent intent, CancellationToken ct)
    {
        p.Method = intent.Method == "card" ? 2 : 1;
        p.Amount = intent.Amount;
        var open = _context.SalesInvoices.AsNoTracking().Include(i => i.Customer).Where(i => !i.IsDeleted && i.PaidAmount < i.TotalAmount);
        List<RomaERP.Domain.Sales.SalesInvoice> found;
        if (!string.IsNullOrWhiteSpace(intent.InvoiceNumber))
        {
            var n = intent.InvoiceNumber.Trim();
            found = await open.Where(i => i.InvoiceNumber.Contains(n)).OrderByDescending(i => i.InvoiceDate).Take(MaxCandidates + 1).ToListAsync(ct);
        }
        else
        {
            var q = Norm(intent.CustomerName);
            if (q.Length == 0) { p.Warnings.Add("ما ذكرتش رقم الفاتورة ولا اسم العميل."); found = new(); }
            else
            {
                var all = await open.OrderByDescending(i => i.InvoiceDate).Take(300).ToListAsync(ct);
                found = all.Where(i => i.Customer is not null && (Norm(i.Customer.NameAr).Contains(q) || Norm(i.Customer.NameEn).Contains(q))).Take(MaxCandidates + 1).ToList();
            }
        }
        p.InvoiceCandidates = found.Take(MaxCandidates).Select(i => new ActionCandidateDto(i.Id, $"{i.InvoiceNumber} - {i.Customer?.NameAr}", $"المتبقي {i.TotalAmount - i.PaidAmount:0.##}")).ToList();
        if (found.Count == 1)
        {
            var inv = found[0];
            p.InvoiceId = inv.Id; p.InvoiceNumber = inv.InvoiceNumber; p.CustomerName = inv.Customer?.NameAr;
            p.OutstandingAmount = inv.TotalAmount - inv.PaidAmount;
            p.Amount ??= p.OutstandingAmount;
        }
        else if (found.Count == 0) p.Warnings.Add("مفيش فاتورة مفتوحة مطابقة.");
        else p.Warnings.Add("في أكتر من فاتورة مفتوحة. اختار الفاتورة الصح.");
        p.Summary = $"تسجيل دفعة{(p.CustomerName is null ? string.Empty : $" من {p.CustomerName}")}.";
    }

    private async Task FillMenuPriceAsync(ActionProposalDto p, ActionIntent intent, CancellationToken ct)
    {
        p.NewPrice = intent.NewPrice;
        var q = Norm(intent.ItemName);
        var items = await _context.Items.AsNoTracking().Where(i => !i.IsDeleted && i.IsMenuItem && !i.IsOpenPrice).ToListAsync(ct);
        var matches = q.Length == 0 ? new() : items.Where(i => Norm(i.NameAr).Contains(q) || Norm(i.NameEn).Contains(q)).ToList();
        var exact = matches.Where(i => Norm(i.NameAr) == q || Norm(i.NameEn) == q).ToList();
        if (exact.Count == 1) matches = exact;
        p.ItemCandidates = matches.Take(MaxCandidates).Select(i => new ActionCandidateDto(i.Id, i.NameAr, $"{i.MenuPrice:0.##}")).ToList();
        if (matches.Count == 1) { p.ItemId = matches[0].Id; p.ItemName = matches[0].NameAr; p.OldPrice = matches[0].MenuPrice; }
        else if (matches.Count == 0) p.Warnings.Add("مفيش صنف منيو بالاسم ده.");
        else p.Warnings.Add("في أكتر من صنف بنفس الاسم. اختار الصنف الصح.");
        if (p.NewPrice is null) p.Warnings.Add("ما ذكرتش السعر الجديد.");
        p.Summary = $"تغيير سعر المنيو{(p.ItemName is null ? string.Empty : $" للصنف {p.ItemName}")}.";
    }

    public async Task<ExecuteActionResultDto> ExecuteAsync(ExecuteActionRequestDto r, CancellationToken ct = default)
    {
        switch (r.Kind)
        {
            case AssistantActionKind.SalesInvoice:
            {
                if (r.CustomerId is null) throw new ValidationAppException("اختار العميل.");
                if (r.Lines.Count == 0 || r.Lines.Count > MaxLines) throw new ValidationAppException("الفاتورة لازم يكون فيها بند واحد على الأقل.");
                if (r.Lines.Any(l => l.UnitPrice is null || l.UnitPrice < 0 || string.IsNullOrWhiteSpace(l.Description) || l.Quantity <= 0))
                    throw new ValidationAppException("كل بند لازم يكون له وصف وكمية وسعر.");
                if (r.PaymentTerm is not (1 or 2 or 3)) throw new ValidationAppException("طريقة الدفع غير صحيحة.");
                var today = DateTime.UtcNow.Date;
                var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.StartDate <= today && x.EndDate >= today && !x.IsClosed, ct)
                    ?? throw new ValidationAppException("مفيش فترة محاسبية مفتوحة لتاريخ النهارده.");
                var invoice = await _sales.CreateInvoiceAsync(new CreateSalesInvoiceDto
                {
                    CustomerId = r.CustomerId.Value,
                    InvoiceDate = today,
                    FiscalPeriodId = period.Id,
                    PaymentTerm = (PaymentTerm)r.PaymentTerm,
                    Notes = string.IsNullOrWhiteSpace(r.Note) ? "أُنشئت بمساعدة المساعد الذكي" : $"{r.Note.Trim()} (بمساعدة المساعد الذكي)",
                    Lines = r.Lines.Select(l => new SalesInvoiceLineInputDto { Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice!.Value }).ToList(),
                }, ct);
                return new ExecuteActionResultDto($"اتعملت الفاتورة {invoice.InvoiceNumber} بإجمالي {invoice.TotalAmount:0.##}.", "/sales/invoices");
            }
            case AssistantActionKind.CustomerPayment:
            {
                if (r.InvoiceId is null) throw new ValidationAppException("اختار الفاتورة.");
                if (r.Amount is null || r.Amount <= 0) throw new ValidationAppException("اكتب مبلغ صحيح.");
                if (r.Method is not (1 or 2)) throw new ValidationAppException("طريقة الدفع غير صحيحة.");
                var invoice = await _sales.RecordPaymentAsync(r.InvoiceId.Value, new RecordSalesPaymentDto
                {
                    Amount = r.Amount.Value, Method = (PaymentTerm)r.Method, PaymentDate = DateTime.UtcNow.Date, Reference = "المساعد الذكي",
                }, ct);
                return new ExecuteActionResultDto($"اتسجلت الدفعة {r.Amount:0.##} على الفاتورة {invoice.InvoiceNumber}.", "/sales/invoices");
            }
            case AssistantActionKind.MenuPrice:
            {
                if (r.ItemId is null) throw new ValidationAppException("اختار الصنف.");
                if (r.NewPrice is null || r.NewPrice < 0) throw new ValidationAppException("اكتب سعر صحيح.");
                var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == r.ItemId && !i.IsDeleted && i.IsMenuItem && !i.IsOpenPrice, ct)
                    ?? throw new NotFoundException(nameof(RomaERP.Domain.Inventory.Item), r.ItemId.Value);
                var old = item.MenuPrice;
                item.MenuPrice = r.NewPrice.Value;
                await _context.SaveChangesAsync(ct);
                return new ExecuteActionResultDto($"سعر {item.NameAr} اتغيّر من {old:0.##} إلى {item.MenuPrice:0.##}.", "/restaurant/menu");
            }
            default:
                throw new ValidationAppException("الطلب ده مش قابل للتنفيذ.");
        }
    }
}

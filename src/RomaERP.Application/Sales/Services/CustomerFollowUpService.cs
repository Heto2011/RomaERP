using System.Net;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.Services;

public class CustomerFollowUpService : ICustomerFollowUpService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSender? _email;

    public CustomerFollowUpService(IApplicationDbContext context, IEmailSender? email = null)
    {
        _context = context;
        _email = email;
    }

    public async Task<List<OverdueCustomerDto>> GetOverdueAsync(DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var today = (asOfDate ?? DateTime.UtcNow).Date;

        var invoices = await _context.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.InstallmentLines)
            .Where(i => (i.PaymentTerm == PaymentTerm.Credit || i.PaymentTerm == PaymentTerm.Installment) && i.TotalAmount > i.PaidAmount)
            .ToListAsync(ct);

        var byCustomer = new Dictionary<Guid, OverdueCustomerDto>();
        foreach (var invoice in invoices)
        {
            var customer = invoice.Customer!;
            var due = DueDate(invoice, customer.PaymentTermsDays);
            var daysOverdue = (today - due).Days;
            if (daysOverdue <= 0) continue;

            if (!byCustomer.TryGetValue(customer.Id, out var row))
            {
                row = new OverdueCustomerDto
                {
                    CustomerId = customer.Id,
                    CustomerCode = customer.Code,
                    CustomerName = customer.NameAr,
                    Phone = string.IsNullOrWhiteSpace(customer.Mobile) ? customer.Phone : customer.Mobile,
                    Email = customer.Email,
                    PaymentTermsDays = customer.PaymentTermsDays
                };
                byCustomer[customer.Id] = row;
            }

            var outstanding = invoice.TotalAmount - invoice.PaidAmount;
            row.Invoices.Add(new OverdueInvoiceDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = due,
                DaysOverdue = daysOverdue,
                Outstanding = outstanding
            });
            row.TotalOverdue += outstanding;
            row.OldestDaysOverdue = Math.Max(row.OldestDaysOverdue, daysOverdue);
        }

        var ids = byCustomer.Keys.ToList();
        var followUps = await _context.CustomerFollowUps.AsNoTracking()
            .Where(f => ids.Contains(f.CustomerId))
            .OrderByDescending(f => f.FollowUpDate).ThenByDescending(f => f.CreatedAtUtc)
            .ToListAsync(ct);
        foreach (var row in byCustomer.Values)
        {
            var last = followUps.FirstOrDefault(f => f.CustomerId == row.CustomerId);
            row.LastFollowUp = last is null ? null : Map(last);
            row.Invoices = row.Invoices.OrderByDescending(i => i.DaysOverdue).ToList();
        }

        return byCustomer.Values.OrderByDescending(r => r.TotalOverdue).ToList();
    }

    public async Task<List<FollowUpDto>> GetFollowUpsAsync(Guid customerId, CancellationToken ct = default)
        => (await _context.CustomerFollowUps.AsNoTracking()
                .Where(f => f.CustomerId == customerId)
                .OrderByDescending(f => f.FollowUpDate).ThenByDescending(f => f.CreatedAtUtc)
                .Take(100).ToListAsync(ct))
            .Select(Map).ToList();

    public async Task<FollowUpDto> AddFollowUpAsync(CreateFollowUpDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Method))
            throw new ValidationAppException("اختار طريقة المتابعة (مكالمة، زيارة، رسالة...).");
        if (dto.Method.Trim().Length > 40)
            throw new ValidationAppException("طريقة المتابعة طويلة.");
        if (dto.Note is { Length: > 500 })
            throw new ValidationAppException("الملاحظة طويلة (أقصى حد 500 حرف).");
        if (!await _context.Customers.AnyAsync(c => c.Id == dto.CustomerId && !c.IsDeleted, ct))
            throw new NotFoundException(nameof(Customer), dto.CustomerId);

        var entity = new CustomerFollowUp
        {
            CustomerId = dto.CustomerId,
            FollowUpDate = dto.FollowUpDate == default ? DateTime.UtcNow.Date : dto.FollowUpDate.Date,
            Method = dto.Method.Trim(),
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            PromisedPayDate = dto.PromisedPayDate?.Date
        };
        _context.CustomerFollowUps.Add(entity);
        await _context.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ReminderEmailResultDto> SendReminderEmailAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId);
        if (string.IsNullOrWhiteSpace(customer.Email))
            return new ReminderEmailResultDto { Sent = false, Message = "العميل ده ملوش إيميل مسجّل. ضيفه من بيانات العميل." };
        if (_email is null || !_email.IsConfigured)
            return new ReminderEmailResultDto { Sent = false, Message = "الإيميل مش متفعّل على السيرفر لسه." };

        var overdue = (await GetOverdueAsync(null, ct)).FirstOrDefault(o => o.CustomerId == customerId);
        if (overdue is null)
            return new ReminderEmailResultDto { Sent = false, Message = "مفيش فواتير متأخرة على العميل ده." };

        var rows = string.Concat(overdue.Invoices.Select(i =>
            $"<tr><td>{WebUtility.HtmlEncode(i.InvoiceNumber)}</td><td>{i.DueDate:yyyy-MM-dd}</td><td>{i.DaysOverdue}</td><td>{i.Outstanding:0.##}</td></tr>"));
        var html = $"<div dir=\"rtl\" style=\"font-family:Tahoma,Arial,sans-serif\"><p>السادة / {WebUtility.HtmlEncode(customer.NameAr)}،</p>" +
                   $"<p>تحية طيبة، نذكّركم بوجود مبالغ مستحقة وتجاوزت موعد السداد بإجمالي <strong>{overdue.TotalOverdue:0.##}</strong>:</p>" +
                   "<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\"><tr><th>الفاتورة</th><th>تاريخ الاستحقاق</th><th>أيام التأخير</th><th>المتبقي</th></tr>" +
                   rows + "</table><p>برجاء السداد في أقرب وقت. وإذا تم السداد بالفعل فنرجو تجاهل هذه الرسالة. شكرًا لتعاونكم.</p></div>";

        var result = await _email.SendAsync(customer.Email, "تذكير بمبالغ مستحقة", html, ct);
        if (!result.Success)
            return new ReminderEmailResultDto { Sent = false, Message = "الإيميل ما اتبعتش: " + (result.FailureReason ?? "سبب غير معروف") };

        await AddFollowUpAsync(new CreateFollowUpDto { CustomerId = customerId, FollowUpDate = DateTime.UtcNow.Date, Method = "إيميل", Note = "تذكير تلقائي بالمتأخرات" }, ct);
        return new ReminderEmailResultDto { Sent = true, Message = "اتبعت التذكير بالإيميل." };
    }

    /// <summary>Credit: invoice date + the customer's agreed days. Installment: the due date of the first installment that
    /// the payments so far do not yet cover (payments are applied to installments in date order).</summary>
    internal static DateTime DueDate(SalesInvoice invoice, int paymentTermsDays)
    {
        if (invoice.PaymentTerm == PaymentTerm.Installment && invoice.InstallmentLines.Count > 0)
        {
            decimal covered = invoice.PaidAmount;
            foreach (var line in invoice.InstallmentLines.OrderBy(l => l.DueDate))
            {
                if (covered < line.Amount) return line.DueDate.Date;
                covered -= line.Amount;
            }
        }
        return invoice.InvoiceDate.Date.AddDays(Math.Max(0, paymentTermsDays));
    }

    private static FollowUpDto Map(CustomerFollowUp f) => new()
    {
        Id = f.Id,
        FollowUpDate = f.FollowUpDate,
        Method = f.Method,
        Note = f.Note,
        PromisedPayDate = f.PromisedPayDate
    };
}

using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Tenancy;

public record ReviewPromptStatus(bool InWindow, string PeriodKey, bool Answered);
public record SubmitReview(Guid TenantId, string CompanyCode, string? CompanyName, string? UserId, string UserName, string? DisplayName, int Rating, string? Comment, bool AllowPublish, bool ShowCompany, string Product);
public record PublicReview(int Rating, string? Comment, string DisplayName, string? CompanyName, string Product, string Month);
public record PublicReviews(int Count, double Average, List<PublicReview> Items);

/// <summary>The end-of-month "what do you think?" feature: who is asked and when, what is stored, what we approve, and what the website may show.</summary>
public class CustomerReviewService
{
    private readonly CentralDbContext _central;
    public CustomerReviewService(CentralDbContext central) => _central = central;

    /// <summary>The last five days of a month and the first three of the next are the asking window; the review belongs to the month that just ended
    /// (or is ending). Outside it a review can still be sent, and counts for the current month.</summary>
    public static (bool InWindow, string PeriodKey) PromptWindow(DateTime todayUtc)
    {
        var day = todayUtc.Day;
        var last = DateTime.DaysInMonth(todayUtc.Year, todayUtc.Month);
        if (day >= last - 4) return (true, todayUtc.ToString("yyyy-MM"));
        if (day <= 3) return (true, todayUtc.AddMonths(-1).ToString("yyyy-MM"));
        return (false, todayUtc.ToString("yyyy-MM"));
    }

    public async Task<ReviewPromptStatus> GetStatusAsync(Guid tenantId, string? userId, DateTime todayUtc, CancellationToken ct = default)
    {
        var (inWindow, key) = PromptWindow(todayUtc);
        var answered = await _central.CustomerReviews.AnyAsync(r => r.TenantId == tenantId && r.UserId == userId && r.PeriodKey == key, ct);
        return new ReviewPromptStatus(inWindow, key, answered);
    }

    public async Task SubmitAsync(SubmitReview request, DateTime todayUtc, CancellationToken ct = default)
    {
        if (request.Rating is < 1 or > 5) throw new ValidationAppException("اختار تقييم من 1 لـ 5 نجوم.");
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment is { Length: > 1000 }) throw new ValidationAppException("التعليق طويل (الحد 1000 حرف).");
        if (request.AllowPublish && comment is null) throw new ValidationAppException("اكتب تعليق لو عايز رأيك يتنشر.");

        var (_, key) = PromptWindow(todayUtc);
        var display = string.IsNullOrWhiteSpace(request.DisplayName) ? request.UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? request.UserName : request.DisplayName.Trim();
        if (display.Length > 80) display = display[..80];

        var existing = await _central.CustomerReviews.FirstOrDefaultAsync(r => r.TenantId == request.TenantId && r.UserId == request.UserId && r.PeriodKey == key, ct);
        if (existing is null)
        {
            existing = new CustomerReview { TenantId = request.TenantId, UserId = request.UserId, PeriodKey = key };
            _central.CustomerReviews.Add(existing);
        }
        existing.CompanyCode = request.CompanyCode;
        existing.CompanyName = request.CompanyName;
        existing.UserName = request.UserName;
        existing.DisplayName = display;
        existing.Rating = request.Rating;
        existing.Comment = comment;
        existing.AllowPublish = request.AllowPublish;
        existing.ShowCompany = request.AllowPublish && request.ShowCompany;
        existing.Product = string.IsNullOrWhiteSpace(request.Product) ? "ERP" : request.Product;
        existing.Status = ReviewStatus.Pending;   // a changed review is looked at again before it can appear
        existing.DecidedAtUtc = null;
        await _central.SaveChangesAsync(ct);
    }

    public async Task<List<CustomerReview>> ListAsync(ReviewStatus? status, CancellationToken ct = default)
        => await _central.CustomerReviews.AsNoTracking().Where(r => status == null || r.Status == status).OrderByDescending(r => r.CreatedAtUtc).Take(300).ToListAsync(ct);

    public async Task DecideAsync(Guid id, ReviewStatus status, string? note, CancellationToken ct = default)
    {
        var review = await _central.CustomerReviews.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(CustomerReview), id);
        if (status == ReviewStatus.Approved && !review.AllowPublish)
            throw new ValidationAppException("العميل ما وافقش على نشر رأيه، فمينفعش يتنشر.");
        review.Status = status;
        review.AdminNote = note?.Trim();
        review.DecidedAtUtc = DateTime.UtcNow;
        await _central.SaveChangesAsync(ct);
    }

    /// <summary>What the website may show: approved reviews whose author agreed to publish them. The average and count are real, over all of those.</summary>
    public async Task<PublicReviews> GetPublicAsync(CancellationToken ct = default)
    {
        var rows = await _central.CustomerReviews.AsNoTracking().Where(r => r.Status == ReviewStatus.Approved && r.AllowPublish).OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);
        return new PublicReviews(rows.Count, rows.Count == 0 ? 0 : Math.Round(rows.Average(r => r.Rating), 1),
            rows.Take(30).Select(r => new PublicReview(r.Rating, r.Comment, r.DisplayName, r.ShowCompany ? r.CompanyName : null, r.Product, r.PeriodKey)).ToList());
    }
}

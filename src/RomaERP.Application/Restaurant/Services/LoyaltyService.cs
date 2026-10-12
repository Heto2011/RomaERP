using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

public class LoyaltySettingsDto
{
    public bool Enabled { get; set; }
    public decimal SpendPerPoint { get; set; }
    public decimal PointValue { get; set; }
    public int MinRedeemPoints { get; set; }
}

public class LoyaltyMemberDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int PointsBalance { get; set; }
    public int LifetimePointsEarned { get; set; }
    public int VisitCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageSpend { get; set; }
    public DateTime? LastVisitUtc { get; set; }
}

public class LoyaltyOrderHistoryDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public RestaurantOrderType OrderType { get; set; }
    public RestaurantOrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }
    public List<string> Items { get; set; } = new();
}

public class LoyaltyTransactionDto
{
    public DateTime CreatedAtUtc { get; set; }
    public LoyaltyTransactionType Type { get; set; }
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public string? Note { get; set; }
}

public class LoyaltyMemberDetailDto : LoyaltyMemberDto
{
    /// <summary>Items ordered most often, with how many times — shows what the customer likes.</summary>
    public List<string> FavouriteItems { get; set; } = new();
    public List<LoyaltyOrderHistoryDto> Orders { get; set; } = new();
    public List<LoyaltyTransactionDto> Transactions { get; set; } = new();
}

public class SaveLoyaltyMemberDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class AdjustLoyaltyPointsDto
{
    /// <summary>Positive to add, negative to take away.</summary>
    public int Points { get; set; }
    public string? Note { get; set; }
}

public interface ILoyaltyService
{
    Task<LoyaltySettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<LoyaltySettingsDto> UpdateSettingsAsync(LoyaltySettingsDto dto, CancellationToken ct = default);
    Task<List<LoyaltyMemberDto>> GetMembersAsync(string? search, CancellationToken ct = default);
    Task<LoyaltyMemberDto?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<LoyaltyMemberDetailDto> GetMemberAsync(Guid id, CancellationToken ct = default);
    Task<LoyaltyMemberDto> CreateMemberAsync(SaveLoyaltyMemberDto dto, CancellationToken ct = default);
    Task<LoyaltyMemberDto> UpdateMemberAsync(Guid id, SaveLoyaltyMemberDto dto, CancellationToken ct = default);
    Task<LoyaltyMemberDto> AdjustPointsAsync(Guid id, AdjustLoyaltyPointsDto dto, CancellationToken ct = default);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly IApplicationDbContext _context;

    public LoyaltyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LoyaltySettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return Map(s);
    }

    public async Task<LoyaltySettingsDto> UpdateSettingsAsync(LoyaltySettingsDto dto, CancellationToken ct = default)
    {
        if (dto.SpendPerPoint <= 0)
            throw new ValidationAppException("مبلغ الصرف لكل نقطة لازم يكون أكبر من صفر.");
        if (dto.PointValue < 0)
            throw new ValidationAppException("قيمة النقطة لا يمكن أن تكون سالبة.");
        if (dto.MinRedeemPoints < 0)
            throw new ValidationAppException("الحد الأدنى للاستبدال لا يمكن أن يكون سالبًا.");

        var s = await _context.CompanySettings.FirstOrDefaultAsync(ct)
            ?? throw new ValidationAppException("إعدادات الشركة غير موجودة.");
        s.LoyaltyEnabled = dto.Enabled;
        s.LoyaltySpendPerPoint = dto.SpendPerPoint;
        s.LoyaltyPointValue = dto.PointValue;
        s.LoyaltyMinRedeemPoints = dto.MinRedeemPoints;
        await _context.SaveChangesAsync(ct);
        return Map(s);
    }

    public async Task<List<LoyaltyMemberDto>> GetMembersAsync(string? search, CancellationToken ct = default)
    {
        var query = _context.LoyaltyMembers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            var digits = LoyaltyPhone.Normalize(text);
            query = query.Where(m => m.Name.Contains(text) || (digits.Length > 0 && m.Phone.Contains(digits)));
        }
        var members = await query.OrderByDescending(m => m.LastVisitUtc).ThenBy(m => m.Name).Take(300).ToListAsync(ct);
        return members.Select(Map).ToList();
    }

    public async Task<LoyaltyMemberDto?> FindByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var digits = LoyaltyPhone.Normalize(phone);
        if (digits.Length < 6) return null;
        var member = await _context.LoyaltyMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Phone == digits, ct);
        return member is null ? null : Map(member);
    }

    public async Task<LoyaltyMemberDetailDto> GetMemberAsync(Guid id, CancellationToken ct = default)
    {
        var member = await _context.LoyaltyMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(LoyaltyMember), id);

        var orders = await _context.RestaurantOrders
            .AsNoTracking()
            .Include(o => o.Lines).ThenInclude(l => l.Item)
            .Where(o => o.LoyaltyMemberId == id && (o.Status == RestaurantOrderStatus.Billed || o.Status == RestaurantOrderStatus.Voided))
            .OrderByDescending(o => o.OrderDate)
            .Take(100)
            .ToListAsync(ct);

        var transactions = await _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyMemberId == id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var favourites = orders
            .Where(o => o.Status == RestaurantOrderStatus.Billed)
            .SelectMany(o => o.Lines)
            .GroupBy(l => l.Item?.NameAr ?? string.Empty)
            .Where(g => g.Key.Length > 0)
            .OrderByDescending(g => g.Sum(l => l.Quantity))
            .Take(5)
            .Select(g => $"{g.Key} × {g.Sum(l => l.Quantity):0.##}")
            .ToList();

        var detail = new LoyaltyMemberDetailDto { FavouriteItems = favourites };
        Fill(detail, member);
        detail.Orders = orders.Select(o => new LoyaltyOrderHistoryDto
        {
            OrderId = o.Id,
            OrderNumber = o.OrderNumber,
            OrderDate = o.OrderDate,
            OrderType = o.OrderType,
            Status = o.Status,
            Total = Math.Max(0, o.Lines.Sum(l => l.LineTotal - l.DiscountAmount) - o.DiscountAmount - o.LoyaltyDiscountAmount),
            PointsEarned = o.LoyaltyPointsEarned,
            PointsRedeemed = o.LoyaltyPointsToRedeem,
            Items = o.Lines.OrderBy(l => l.LineNumber).Select(l => $"{l.Item?.NameAr} × {l.Quantity:0.##}").ToList()
        }).ToList();
        detail.Transactions = transactions.Select(t => new LoyaltyTransactionDto
        {
            CreatedAtUtc = t.CreatedAtUtc, Type = t.Type, Points = t.Points, BalanceAfter = t.BalanceAfter, Note = t.Note
        }).ToList();
        return detail;
    }

    public async Task<LoyaltyMemberDto> CreateMemberAsync(SaveLoyaltyMemberDto dto, CancellationToken ct = default)
    {
        var phone = ValidatePhone(dto.Phone);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationAppException("اكتب اسم العميل.");
        if (await _context.LoyaltyMembers.AnyAsync(m => m.Phone == phone, ct))
            throw new ValidationAppException("رقم الموبايل ده مسجل لعميل تاني.");

        var member = new LoyaltyMember { Name = dto.Name.Trim(), Phone = phone, Notes = dto.Notes?.Trim() };
        _context.LoyaltyMembers.Add(member);
        await _context.SaveChangesAsync(ct);
        return Map(member);
    }

    public async Task<LoyaltyMemberDto> UpdateMemberAsync(Guid id, SaveLoyaltyMemberDto dto, CancellationToken ct = default)
    {
        var member = await _context.LoyaltyMembers.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(LoyaltyMember), id);
        var phone = ValidatePhone(dto.Phone);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationAppException("اكتب اسم العميل.");
        if (await _context.LoyaltyMembers.AnyAsync(m => m.Phone == phone && m.Id != id, ct))
            throw new ValidationAppException("رقم الموبايل ده مسجل لعميل تاني.");

        member.Name = dto.Name.Trim();
        member.Phone = phone;
        member.Notes = dto.Notes?.Trim();
        await _context.SaveChangesAsync(ct);
        return Map(member);
    }

    public async Task<LoyaltyMemberDto> AdjustPointsAsync(Guid id, AdjustLoyaltyPointsDto dto, CancellationToken ct = default)
    {
        var member = await _context.LoyaltyMembers.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(LoyaltyMember), id);
        if (dto.Points == 0)
            throw new ValidationAppException("اكتب عدد النقاط (موجب للإضافة وسالب للخصم).");
        if (member.PointsBalance + dto.Points < 0)
            throw new ValidationAppException("الخصم أكبر من رصيد النقاط الحالي.");

        member.PointsBalance += dto.Points;
        if (dto.Points > 0) member.LifetimePointsEarned += dto.Points;
        _context.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyMemberId = member.Id,
            Type = LoyaltyTransactionType.Adjust,
            Points = dto.Points,
            BalanceAfter = member.PointsBalance,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? "تعديل يدوي" : dto.Note.Trim()
        });
        await _context.SaveChangesAsync(ct);
        return Map(member);
    }

    private static string ValidatePhone(string raw)
    {
        var phone = LoyaltyPhone.Normalize(raw);
        if (phone.Length < 6)
            throw new ValidationAppException("اكتب رقم موبايل صحيح للعميل.");
        return phone;
    }

    private static LoyaltySettingsDto Map(RomaERP.Domain.Tenancy.CompanySettings? s) => new()
    {
        Enabled = s?.LoyaltyEnabled ?? false,
        SpendPerPoint = s?.LoyaltySpendPerPoint ?? 10m,
        PointValue = s?.LoyaltyPointValue ?? 0.5m,
        MinRedeemPoints = s?.LoyaltyMinRedeemPoints ?? 20
    };

    private static LoyaltyMemberDto Map(LoyaltyMember m)
    {
        var dto = new LoyaltyMemberDto();
        Fill(dto, m);
        return dto;
    }

    private static void Fill(LoyaltyMemberDto dto, LoyaltyMember m)
    {
        dto.Id = m.Id;
        dto.Name = m.Name;
        dto.Phone = m.Phone;
        dto.Notes = m.Notes;
        dto.PointsBalance = m.PointsBalance;
        dto.LifetimePointsEarned = m.LifetimePointsEarned;
        dto.VisitCount = m.VisitCount;
        dto.TotalSpent = m.TotalSpent;
        dto.AverageSpend = m.VisitCount > 0 ? Math.Round(m.TotalSpent / m.VisitCount, 2) : 0;
        dto.LastVisitUtc = m.LastVisitUtc;
    }
}

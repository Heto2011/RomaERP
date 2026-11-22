using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Restaurant.Services;

public class QrOrderingSettingsDto
{
    public bool Enabled { get; set; }
    public Guid? WarehouseId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int RadiusMeters { get; set; } = 50;
    public bool HasLocation { get; set; }
}

public record QrTableCodeDto(Guid TableId, string Number, string? SectionName, string Code);
public record QrOrderLineInput(Guid ItemId, decimal Quantity, string? Notes);
public record QrOrderSubmitInput(string Table, string Code, double? Latitude, double? Longitude, double? AccuracyMeters, string? Note, List<QrOrderLineInput> Lines);
public record QrOrderLineDto(string ItemName, decimal Quantity, decimal UnitPrice, string? Notes);
public record QrOrderRequestDto(Guid Id, string TableNumber, QrOrderStatus Status, int DistanceMeters, string? Note, DateTime CreatedAtUtc, List<QrOrderLineDto> Lines);
public record QrOrderStatusDto(QrOrderStatus Status);

public interface IQrOrderingService
{
    Task<QrOrderingSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<QrOrderingSettingsDto> SaveSettingsAsync(QrOrderingSettingsDto dto, CancellationToken ct = default);
    /// <summary>Stores the restaurant's position (taken from the staff member's phone or screen).</summary>
    Task<QrOrderingSettingsDto> SetLocationAsync(double latitude, double longitude, CancellationToken ct = default);
    /// <summary>Makes every printed table code stop working and returns the new ones.</summary>
    Task<List<QrTableCodeDto>> RotateCodesAsync(CancellationToken ct = default);
    Task<List<QrTableCodeDto>> GetTableCodesAsync(CancellationToken ct = default);
    /// <summary>Public: true when the code belongs to this table and ordering is on.</summary>
    Task<bool> CanOrderAsync(string tableNumber, string? code, CancellationToken ct = default);
    /// <summary>Public: checks code, position and limits, then saves the request as Pending. Returns its id.</summary>
    Task<Guid> SubmitAsync(QrOrderSubmitInput input, string deviceKey, CancellationToken ct = default);
    Task<QrOrderStatusDto?> GetStatusAsync(Guid id, CancellationToken ct = default);
    Task<List<QrOrderRequestDto>> GetPendingAsync(CancellationToken ct = default);
    Task<Guid> ApproveAsync(Guid id, CancellationToken ct = default);
    Task RejectAsync(Guid id, CancellationToken ct = default);
}

public class QrOrderingService : IQrOrderingService
{
    public const int MaxLines = 30;
    public const decimal MaxQuantity = 20;
    public const int MaxPendingPerTable = 3;
    public const int MaxPendingPerDevice = 3;
    public const int MaxPerDeviceLastHour = 10;
    /// <summary>A phone that cannot place itself better than this is refused: the distance would mean nothing.</summary>
    public const double MaxAccuracyMeters = 150;

    private readonly IApplicationDbContext _context;
    private readonly IRestaurantService _restaurant;
    private readonly ICurrentUserService _user;

    public QrOrderingService(IApplicationDbContext context, IRestaurantService restaurant, ICurrentUserService user)
    {
        _context = context;
        _restaurant = restaurant;
        _user = user;
    }

    private async Task<CompanySettings> LoadSettingsAsync(CancellationToken ct) =>
        await _context.CompanySettings.FirstOrDefaultAsync(ct) ?? throw new ValidationAppException("بيانات الشركة لسه ما اتسجلتش.");

    private static QrOrderingSettingsDto ToDto(CompanySettings s) => new()
    {
        Enabled = s.QrOrderingEnabled,
        WarehouseId = s.QrOrderWarehouseId,
        Latitude = s.RestaurantLatitude,
        Longitude = s.RestaurantLongitude,
        RadiusMeters = s.QrOrderRadiusMeters,
        HasLocation = s.RestaurantLatitude.HasValue && s.RestaurantLongitude.HasValue,
    };

    public async Task<QrOrderingSettingsDto> GetSettingsAsync(CancellationToken ct = default) => ToDto(await LoadSettingsAsync(ct));

    public async Task<QrOrderingSettingsDto> SaveSettingsAsync(QrOrderingSettingsDto dto, CancellationToken ct = default)
    {
        var s = await LoadSettingsAsync(ct);
        if (dto.RadiusMeters is < 10 or > 500) throw new ValidationAppException("المسافة لازم تكون بين 10 و 500 متر.");
        if (dto.Enabled)
        {
            if (s.RestaurantLatitude is null || s.RestaurantLongitude is null)
                throw new ValidationAppException("حدد موقع المطعم الأول قبل ما تفعّل الطلب من الموبايل.");
            if (dto.WarehouseId is null || !await _context.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct))
                throw new ValidationAppException("اختار الفرع اللي الطلبات هتتسحب منه.");
        }
        s.QrOrderingEnabled = dto.Enabled;
        s.QrOrderWarehouseId = dto.WarehouseId;
        s.QrOrderRadiusMeters = dto.RadiusMeters;
        if (s.QrSecret is null) { s.QrSecret = NewSecret(); s.QrSecretChangedAtUtc = DateTime.UtcNow; }
        await _context.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<QrOrderingSettingsDto> SetLocationAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) throw new ValidationAppException("الموقع مش صحيح.");
        var s = await LoadSettingsAsync(ct);
        s.RestaurantLatitude = latitude;
        s.RestaurantLongitude = longitude;
        await _context.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>The code for one table: a keyed hash of the table's id, so it cannot be guessed or worked out for another table.</summary>
    public static string MakeCode(string secret, Guid tableId)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(tableId.ToByteArray());
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=')[..16];
    }

    public async Task<List<QrTableCodeDto>> RotateCodesAsync(CancellationToken ct = default)
    {
        var s = await LoadSettingsAsync(ct);
        s.QrSecret = NewSecret();
        s.QrSecretChangedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return await GetTableCodesAsync(ct);
    }

    public async Task<List<QrTableCodeDto>> GetTableCodesAsync(CancellationToken ct = default)
    {
        var s = await LoadSettingsAsync(ct);
        if (s.QrSecret is null)
        {
            s.QrSecret = NewSecret();
            s.QrSecretChangedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        var secret = s.QrSecret!;
        var tables = await _context.RestaurantTables.Where(t => !t.IsDeleted).OrderBy(t => t.Number).ToListAsync(ct);
        return tables.Select(t => new QrTableCodeDto(t.Id, t.Number, t.SectionName, MakeCode(secret, t.Id))).ToList();
    }

    private async Task<(RestaurantTable table, CompanySettings settings)?> ResolveAsync(string tableNumber, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tableNumber) || string.IsNullOrWhiteSpace(code)) return null;
        var s = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null || !s.QrOrderingEnabled || s.QrSecret is null) return null;
        var number = tableNumber.Trim();
        var table = await _context.RestaurantTables.AsNoTracking().FirstOrDefaultAsync(t => t.Number == number && !t.IsDeleted, ct);
        if (table is null) return null;
        var expected = Encoding.UTF8.GetBytes(MakeCode(s.QrSecret, table.Id));
        var given = Encoding.UTF8.GetBytes(code.Trim());
        return CryptographicOperations.FixedTimeEquals(expected, given) ? (table, s) : null;
    }

    public async Task<bool> CanOrderAsync(string tableNumber, string? code, CancellationToken ct = default) =>
        await ResolveAsync(tableNumber, code, ct) is not null;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000;
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public async Task<Guid> SubmitAsync(QrOrderSubmitInput input, string deviceKey, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(input.Table, input.Code, ct)
            ?? throw new ValidationAppException("الكود ده مش شغال. اسكان الكود اللي على الطاولة تاني.");
        var table = resolved.table; var s = resolved.settings;

        if (input.Latitude is null || input.Longitude is null)
            throw new ValidationAppException("لازم تسمح بتحديد موقعك علشان تقدر تطلب.");
        if (input.AccuracyMeters is null || input.AccuracyMeters > MaxAccuracyMeters)
            throw new ValidationAppException("موقعك مش واضح كفاية. قرّب من الشباك أو شغّل الـGPS وجرّب تاني.");
        var distance = DistanceMeters(input.Latitude.Value, input.Longitude.Value, s.RestaurantLatitude!.Value, s.RestaurantLongitude!.Value);
        if (distance > s.QrOrderRadiusMeters + 0.0)
            throw new ValidationAppException("الطلب بيتقبل من جوه المطعم بس.");

        if (input.Lines is null || input.Lines.Count == 0) throw new ValidationAppException("السلة فاضية.");
        if (input.Lines.Count > MaxLines) throw new ValidationAppException("عدد الأصناف كبير. كلّم الكاشير.");
        if (input.Lines.Any(l => l.Quantity <= 0 || l.Quantity > MaxQuantity)) throw new ValidationAppException("الكمية مش صحيحة.");

        var device = deviceKey.Length > 64 ? deviceKey[..64] : deviceKey;
        var pendingForTable = await _context.QrOrderRequests.CountAsync(r => r.TableId == table.Id && r.Status == QrOrderStatus.Pending, ct);
        if (pendingForTable >= MaxPendingPerTable) throw new ValidationAppException("في طلبات على الطاولة دي لسه مستنية الكاشير. استنى شوية.");
        var pendingForDevice = await _context.QrOrderRequests.CountAsync(r => r.DeviceKey == device && r.Status == QrOrderStatus.Pending, ct);
        if (pendingForDevice >= MaxPendingPerDevice) throw new ValidationAppException("عندك طلبات لسه مستنية الكاشير. استنى شوية.");
        var since = DateTime.UtcNow.AddHours(-1);
        if (await _context.QrOrderRequests.CountAsync(r => r.DeviceKey == device && r.CreatedAtUtc >= since, ct) >= MaxPerDeviceLastHour)
            throw new ValidationAppException("طلبات كتير في وقت قصير. كلّم الكاشير.");

        var ids = input.Lines.Select(l => l.ItemId).Distinct().ToList();
        var menu = (await _restaurant.GetMenuAsync(ct)).Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id);
        var request = new QrOrderRequest
        {
            TableId = table.Id,
            TableNumber = table.Number,
            DistanceMeters = (int)Math.Round(distance),
            DeviceKey = device,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : (input.Note.Trim().Length > 300 ? input.Note.Trim()[..300] : input.Note.Trim()),
        };
        foreach (var l in input.Lines)
        {
            if (!menu.TryGetValue(l.ItemId, out var item)) throw new ValidationAppException("في صنف مش موجود في المنيو.");
            if (item.IsOpenPrice) throw new ValidationAppException($"الصنف \"{item.NameAr}\" بسعر مفتوح، اطلبه من الكاشير.");
            request.Lines.Add(new QrOrderRequestLine
            {
                ItemId = item.Id,
                ItemName = item.NameAr,
                Quantity = l.Quantity,
                UnitPrice = item.MenuPrice,
                Notes = string.IsNullOrWhiteSpace(l.Notes) ? null : (l.Notes.Trim().Length > 200 ? l.Notes.Trim()[..200] : l.Notes.Trim()),
            });
        }
        _context.QrOrderRequests.Add(request);
        await _context.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task<QrOrderStatusDto?> GetStatusAsync(Guid id, CancellationToken ct = default)
    {
        var status = await _context.QrOrderRequests.AsNoTracking().Where(r => r.Id == id).Select(r => (QrOrderStatus?)r.Status).FirstOrDefaultAsync(ct);
        return status is null ? null : new QrOrderStatusDto(status.Value);
    }

    public async Task<List<QrOrderRequestDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var rows = await _context.QrOrderRequests.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.Status == QrOrderStatus.Pending).OrderBy(r => r.CreatedAtUtc).Take(50).ToListAsync(ct);
        return rows.Select(r => new QrOrderRequestDto(r.Id, r.TableNumber, r.Status, r.DistanceMeters, r.Note, r.CreatedAtUtc,
            r.Lines.Select(l => new QrOrderLineDto(l.ItemName, l.Quantity, l.UnitPrice, l.Notes)).ToList())).ToList();
    }

    public async Task<Guid> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var request = await _context.QrOrderRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(QrOrderRequest), id);
        if (request.Status != QrOrderStatus.Pending) throw new ValidationAppException("الطلب ده اتراجع قبل كده.");
        var s = await LoadSettingsAsync(ct);
        if (s.QrOrderWarehouseId is null) throw new ValidationAppException("اختار الفرع في إعدادات الطلب من الموبايل.");

        // Add to the table's open order if it already has one, otherwise open a new one.
        var orderId = await _context.RestaurantOrders
            .Where(o => o.TableId == request.TableId && o.Status == RestaurantOrderStatus.Open)
            .OrderByDescending(o => o.OrderDate).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
        if (orderId is null)
        {
            var created = await _restaurant.CreateOrderAsync(new CreateRestaurantOrderDto
            {
                OrderType = RestaurantOrderType.DineIn,
                TableId = request.TableId,
                WarehouseId = s.QrOrderWarehouseId.Value,
                Notes = "طلب من الموبايل",
            }, ct);
            orderId = created.Id;
        }
        foreach (var l in request.Lines)
            await _restaurant.AddLineAsync(orderId.Value, new AddOrderLineDto { ItemId = l.ItemId, Quantity = l.Quantity, Notes = l.Notes }, ct);

        request.Status = QrOrderStatus.Approved;
        request.RestaurantOrderId = orderId;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.DecidedByUserId = _user.UserId;
        await _context.SaveChangesAsync(ct);
        return orderId.Value;
    }

    public async Task RejectAsync(Guid id, CancellationToken ct = default)
    {
        var request = await _context.QrOrderRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(QrOrderRequest), id);
        if (request.Status != QrOrderStatus.Pending) throw new ValidationAppException("الطلب ده اتراجع قبل كده.");
        request.Status = QrOrderStatus.Rejected;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.DecidedByUserId = _user.UserId;
        await _context.SaveChangesAsync(ct);
    }
}

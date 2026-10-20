using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

public interface IReservationService
{
    Task<List<ReservationDto>> GetAsync(DateTime? date, ReservationStatus? status, CancellationToken ct = default);
    Task<ReservationDto> CreateAsync(SaveReservationDto dto, CancellationToken ct = default);
    Task<ReservationDto> UpdateAsync(Guid id, SaveReservationDto dto, CancellationToken ct = default);
    Task<ReservationDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task<ReservationDto> MarkNoShowAsync(Guid id, CancellationToken ct = default);
    Task<SeatReservationResultDto> SeatAsync(Guid id, SeatReservationDto dto, CancellationToken ct = default);
}

public class ReservationService : IReservationService
{
    /// <summary>How long a booking holds its table: another booking on the same table closer than this is a clash.</summary>
    public static readonly TimeSpan TableHold = TimeSpan.FromHours(2);

    private readonly IApplicationDbContext _context;
    private readonly IRestaurantService _restaurant;

    public ReservationService(IApplicationDbContext context, IRestaurantService restaurant)
    {
        _context = context;
        _restaurant = restaurant;
    }

    public async Task<List<ReservationDto>> GetAsync(DateTime? date, ReservationStatus? status, CancellationToken ct = default)
    {
        var query = _context.RestaurantReservations.AsNoTracking()
            .Include(r => r.Table).Include(r => r.RestaurantOrder).AsQueryable();
        if (date is { } d)
        {
            var from = d.Date;
            var to = from.AddDays(1);
            query = query.Where(r => r.ReservationAt >= from && r.ReservationAt < to);
        }
        if (status is { } s) query = query.Where(r => r.Status == s);
        var rows = await query.OrderBy(r => r.ReservationAt).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<ReservationDto> CreateAsync(SaveReservationDto dto, CancellationToken ct = default)
    {
        var (name, phone, notes) = await ValidateAsync(dto, excludeId: null, ct);
        var count = await _context.RestaurantReservations.IgnoreQueryFilters().CountAsync(ct);
        var reservation = new RestaurantReservation
        {
            ReservationNumber = $"RS-{(count + 1):D6}",
            CustomerName = name,
            CustomerPhone = phone,
            PartySize = dto.PartySize,
            ReservationAt = dto.ReservationAt,
            TableId = dto.TableId,
            Notes = notes,
            Status = ReservationStatus.Booked
        };
        _context.RestaurantReservations.Add(reservation);
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(reservation.Id, ct);
    }

    public async Task<ReservationDto> UpdateAsync(Guid id, SaveReservationDto dto, CancellationToken ct = default)
    {
        var reservation = await LoadAsync(id, ct);
        EnsureBooked(reservation);
        var (name, phone, notes) = await ValidateAsync(dto, excludeId: id, ct);
        reservation.CustomerName = name;
        reservation.CustomerPhone = phone;
        reservation.PartySize = dto.PartySize;
        reservation.ReservationAt = dto.ReservationAt;
        reservation.TableId = dto.TableId;
        reservation.Notes = notes;
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(id, ct);
    }

    public async Task<ReservationDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await LoadAsync(id, ct);
        EnsureBooked(reservation);
        reservation.Status = ReservationStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(id, ct);
    }

    public async Task<ReservationDto> MarkNoShowAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await LoadAsync(id, ct);
        EnsureBooked(reservation);
        reservation.Status = ReservationStatus.NoShow;
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(id, ct);
    }

    public async Task<SeatReservationResultDto> SeatAsync(Guid id, SeatReservationDto dto, CancellationToken ct = default)
    {
        var reservation = await LoadAsync(id, ct);
        EnsureBooked(reservation);
        if (dto.OrderType != RestaurantOrderType.DineIn && dto.OrderType != RestaurantOrderType.Takeaway)
            throw new ValidationAppException("اختار محلي أو سفري.");

        var order = await _restaurant.CreateOrderAsync(new CreateRestaurantOrderDto
        {
            OrderType = dto.OrderType,
            TableId = dto.OrderType == RestaurantOrderType.DineIn ? dto.TableId ?? reservation.TableId : null,
            CustomerName = reservation.CustomerName,
            CustomerPhone = reservation.CustomerPhone,
            WaiterEmployeeId = dto.WaiterEmployeeId,
            WarehouseId = dto.WarehouseId,
            Notes = $"حجز {reservation.ReservationNumber}" + (string.IsNullOrWhiteSpace(reservation.Notes) ? "" : $" - {reservation.Notes}")
        }, ct);

        reservation.Status = ReservationStatus.Seated;
        reservation.RestaurantOrderId = order.Id;
        await _context.SaveChangesAsync(ct);

        return new SeatReservationResultDto { Reservation = await GetOneAsync(id, ct), Order = order };
    }

    // ---------- helpers ----------

    private async Task<(string Name, string Phone, string? Notes)> ValidateAsync(SaveReservationDto dto, Guid? excludeId, CancellationToken ct)
    {
        var name = TextLimit.Check(dto.CustomerName, 200, "اسم العميل") ?? throw new ValidationAppException("اكتب اسم العميل.");
        var phone = TextLimit.Check(dto.CustomerPhone, 30, "رقم الموبايل") ?? throw new ValidationAppException("اكتب رقم موبايل العميل.");
        var notes = TextLimit.Check(dto.Notes, 1000, "الملاحظات");

        if (dto.PartySize < 1 || dto.PartySize > 500)
            throw new ValidationAppException("عدد الأشخاص لازم يكون من 1 لـ 500.");
        if (dto.ReservationAt < DateTime.UtcNow.AddDays(-1) || dto.ReservationAt > DateTime.UtcNow.AddYears(1))
            throw new ValidationAppException("تاريخ الحجز غير صحيح.");

        if (dto.TableId is { } tableId)
        {
            var table = await _context.RestaurantTables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tableId, ct)
                ?? throw new NotFoundException(nameof(RestaurantTable), tableId);
            if (dto.PartySize > table.Capacity)
                throw new ValidationAppException($"الطاولة {table.Number} بتسع {table.Capacity} أشخاص بس.");

            var from = dto.ReservationAt - TableHold;
            var to = dto.ReservationAt + TableHold;
            var clash = await _context.RestaurantReservations.AsNoTracking()
                .Where(r => r.TableId == tableId && r.Status == ReservationStatus.Booked && r.Id != excludeId
                            && r.ReservationAt > from && r.ReservationAt < to)
                .OrderBy(r => r.ReservationAt).FirstOrDefaultAsync(ct);
            if (clash is not null)
                throw new ValidationAppException($"الطاولة {table.Number} محجوزة الساعة {clash.ReservationAt:HH:mm} ({clash.CustomerName}). اختار طاولة تانية أو وقت تاني.");
        }
        return (name, phone, notes);
    }

    private static void EnsureBooked(RestaurantReservation reservation)
    {
        if (reservation.Status != ReservationStatus.Booked)
            throw new ValidationAppException("الحجز ده مش في حالة \"محجوز\" — مينفعش يتغير.");
    }

    private async Task<RestaurantReservation> LoadAsync(Guid id, CancellationToken ct)
        => await _context.RestaurantReservations.FirstOrDefaultAsync(r => r.Id == id, ct)
           ?? throw new NotFoundException(nameof(RestaurantReservation), id);

    private async Task<ReservationDto> GetOneAsync(Guid id, CancellationToken ct)
    {
        var r = await _context.RestaurantReservations.AsNoTracking()
            .Include(x => x.Table).Include(x => x.RestaurantOrder).FirstAsync(x => x.Id == id, ct);
        return Map(r);
    }

    private static ReservationDto Map(RestaurantReservation r) => new()
    {
        Id = r.Id, ReservationNumber = r.ReservationNumber, CustomerName = r.CustomerName, CustomerPhone = r.CustomerPhone,
        PartySize = r.PartySize, ReservationAt = r.ReservationAt, TableId = r.TableId, TableNumber = r.Table?.Number,
        Status = r.Status, Notes = r.Notes, RestaurantOrderId = r.RestaurantOrderId, OrderNumber = r.RestaurantOrder?.OrderNumber
    };
}

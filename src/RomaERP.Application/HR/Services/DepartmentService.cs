using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IApplicationDbContext _context;

    public DepartmentService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DepartmentDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Departments
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Code)
            .Select(d => Map(d))
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dept = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException(nameof(Department), id);
        return Map(dept);
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.Departments.AnyAsync(d => d.Code == dto.Code && !d.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود القسم '{dto.Code}' مستخدم بالفعل.");

        var department = new Department
        {
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            ParentDepartmentId = dto.ParentDepartmentId,
            ManagerId = dto.ManagerId,
            IsActive = true
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(ct);
        return Map(department);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid id, CreateDepartmentDto dto, CancellationToken ct = default)
    {
        var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException(nameof(Department), id);

        department.NameAr = dto.NameAr.Trim();
        department.NameEn = dto.NameEn.Trim();
        department.ParentDepartmentId = dto.ParentDepartmentId;
        department.ManagerId = dto.ManagerId;

        await _context.SaveChangesAsync(ct);
        return Map(department);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException(nameof(Department), id);

        var hasEmployees = await _context.Employees.AnyAsync(e => e.DepartmentId == id && !e.IsDeleted, ct);
        if (hasEmployees)
            throw new ValidationAppException("لا يمكن حذف قسم به موظفين.");

        department.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static DepartmentDto Map(Department d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        NameAr = d.NameAr,
        NameEn = d.NameEn,
        ParentDepartmentId = d.ParentDepartmentId,
        ManagerId = d.ManagerId,
        IsActive = d.IsActive
    };
}

public class PositionService : IPositionService
{
    private readonly IApplicationDbContext _context;

    public PositionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PositionDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Positions
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Code)
            .Select(p => Map(p))
            .ToListAsync(ct);
    }

    public async Task<PositionDto> CreateAsync(CreatePositionDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.Positions.AnyAsync(p => p.Code == dto.Code && !p.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود الوظيفة '{dto.Code}' مستخدم بالفعل.");

        var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId, ct)
            ?? throw new NotFoundException(nameof(Department), dto.DepartmentId);

        var position = new Position
        {
            Code = dto.Code.Trim(),
            TitleAr = dto.TitleAr.Trim(),
            TitleEn = dto.TitleEn.Trim(),
            DepartmentId = department.Id,
            IsActive = true
        };

        _context.Positions.Add(position);
        await _context.SaveChangesAsync(ct);
        return Map(position);
    }

    public async Task<PositionDto> UpdateAsync(Guid id, CreatePositionDto dto, CancellationToken ct = default)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Position), id);

        position.TitleAr = dto.TitleAr.Trim();
        position.TitleEn = dto.TitleEn.Trim();
        position.DepartmentId = dto.DepartmentId;

        await _context.SaveChangesAsync(ct);
        return Map(position);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Position), id);

        var hasEmployees = await _context.Employees.AnyAsync(e => e.PositionId == id && !e.IsDeleted, ct);
        if (hasEmployees)
            throw new ValidationAppException("لا يمكن حذف وظيفة مرتبطة بموظفين.");

        position.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static PositionDto Map(Position p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        TitleAr = p.TitleAr,
        TitleEn = p.TitleEn,
        DepartmentId = p.DepartmentId,
        IsActive = p.IsActive
    };
}

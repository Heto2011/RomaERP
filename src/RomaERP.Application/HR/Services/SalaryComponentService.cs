using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class SalaryComponentService : ISalaryComponentService
{
    private readonly IApplicationDbContext _context;

    public SalaryComponentService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SalaryComponentDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.SalaryComponents
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Code)
            .Select(c => Map(c))
            .ToListAsync(ct);
    }

    public async Task<SalaryComponentDto> CreateAsync(CreateSalaryComponentDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.SalaryComponents.AnyAsync(c => c.Code == dto.Code && !c.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود عنصر الراتب '{dto.Code}' مستخدم بالفعل.");

        if (dto.LinkedAccountId is { } accountId)
        {
            var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId && !a.IsDeleted, ct);
            if (!accountExists)
                throw new ValidationAppException("الحساب المرتبط غير موجود.");
        }

        var component = new SalaryComponent
        {
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            ComponentType = dto.ComponentType,
            CalculationType = dto.CalculationType,
            DefaultValue = dto.DefaultValue,
            IsTaxable = dto.IsTaxable,
            LinkedAccountId = dto.LinkedAccountId,
            IsActive = true
        };

        _context.SalaryComponents.Add(component);
        await _context.SaveChangesAsync(ct);
        return Map(component);
    }

    public async Task AssignToEmployeeAsync(Guid employeeId, Guid salaryComponentId, decimal value, CancellationToken ct = default)
    {
        var employeeExists = await _context.Employees.AnyAsync(e => e.Id == employeeId && !e.IsDeleted, ct);
        if (!employeeExists)
            throw new NotFoundException(nameof(Employee), employeeId);

        var componentExists = await _context.SalaryComponents.AnyAsync(c => c.Id == salaryComponentId && !c.IsDeleted, ct);
        if (!componentExists)
            throw new NotFoundException(nameof(SalaryComponent), salaryComponentId);

        var existing = await _context.EmployeeSalaryComponents
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.SalaryComponentId == salaryComponentId, ct);

        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            _context.EmployeeSalaryComponents.Add(new EmployeeSalaryComponent
            {
                EmployeeId = employeeId,
                SalaryComponentId = salaryComponentId,
                Value = value
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveFromEmployeeAsync(Guid employeeId, Guid salaryComponentId, CancellationToken ct = default)
    {
        var existing = await _context.EmployeeSalaryComponents
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.SalaryComponentId == salaryComponentId, ct);

        if (existing is null)
            return;

        _context.EmployeeSalaryComponents.Remove(existing);
        await _context.SaveChangesAsync(ct);
    }

    private static SalaryComponentDto Map(SalaryComponent c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        NameAr = c.NameAr,
        NameEn = c.NameEn,
        ComponentType = c.ComponentType,
        CalculationType = c.CalculationType,
        DefaultValue = c.DefaultValue,
        IsTaxable = c.IsTaxable,
        LinkedAccountId = c.LinkedAccountId,
        IsActive = c.IsActive
    };
}

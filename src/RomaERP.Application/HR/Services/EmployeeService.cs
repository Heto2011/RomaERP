using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IApplicationDbContext _context;

    public EmployeeService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<EmployeeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Position)
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync(ct);

        return employees.Select(Map).ToList();
    }

    public async Task<EmployeeDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(Employee), id);

        return Map(employee);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.Employees.AnyAsync(e => e.EmployeeCode == dto.EmployeeCode && !e.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود الموظف '{dto.EmployeeCode}' مستخدم بالفعل.");

        await ValidateDepartmentAndPosition(dto.DepartmentId, dto.PositionId, ct);

        var employee = new Employee
        {
            EmployeeCode = dto.EmployeeCode.Trim(),
            FullNameAr = dto.FullNameAr.Trim(),
            FullNameEn = dto.FullNameEn.Trim(),
            NationalId = dto.NationalId,
            BirthDate = dto.BirthDate,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            HireDate = dto.HireDate,
            DepartmentId = dto.DepartmentId,
            PositionId = dto.PositionId,
            BasicSalary = dto.BasicSalary,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            BankAccountNumber = dto.BankAccountNumber,
            Iban = dto.Iban,
            EmploymentStatus = EmploymentStatus.Active
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(employee.Id, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(Employee), id);

        await ValidateDepartmentAndPosition(dto.DepartmentId, dto.PositionId, ct);

        employee.FullNameAr = dto.FullNameAr.Trim();
        employee.FullNameEn = dto.FullNameEn.Trim();
        employee.NationalId = dto.NationalId;
        employee.BirthDate = dto.BirthDate;
        employee.Gender = dto.Gender;
        employee.MaritalStatus = dto.MaritalStatus;
        employee.HireDate = dto.HireDate;
        employee.DepartmentId = dto.DepartmentId;
        employee.PositionId = dto.PositionId;
        employee.BasicSalary = dto.BasicSalary;
        employee.Email = dto.Email;
        employee.Phone = dto.Phone;
        employee.Address = dto.Address;
        employee.BankAccountNumber = dto.BankAccountNumber;
        employee.Iban = dto.Iban;
        employee.EmploymentStatus = dto.EmploymentStatus;
        employee.TerminationDate = dto.TerminationDate;

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(Employee), id);

        var hasPayroll = await _context.PayrollRunLines.AnyAsync(l => l.EmployeeId == id, ct);
        if (hasPayroll)
            throw new ValidationAppException("لا يمكن حذف موظف له سجلات رواتب.");

        employee.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<EmployeeDto?> GetMyProfileAsync(Guid applicationUserId, CancellationToken ct = default)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.ApplicationUserId == applicationUserId, ct);

        return employee is null ? null : Map(employee);
    }

    public async Task<EmployeeDto> LinkUserAsync(Guid employeeId, Guid? applicationUserId, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        if (applicationUserId is { } userId)
        {
            var previouslyLinked = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == userId && e.Id != employeeId, ct);
            if (previouslyLinked is not null)
                previouslyLinked.ApplicationUserId = null;
        }

        employee.ApplicationUserId = applicationUserId;
        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(employeeId, ct);
    }

    private async Task ValidateDepartmentAndPosition(Guid departmentId, Guid positionId, CancellationToken ct)
    {
        var departmentExists = await _context.Departments.AnyAsync(d => d.Id == departmentId && !d.IsDeleted, ct);
        if (!departmentExists)
            throw new NotFoundException(nameof(Department), departmentId);

        var position = await _context.Positions.FirstOrDefaultAsync(p => p.Id == positionId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Position), positionId);

        if (position.DepartmentId != departmentId)
            throw new ValidationAppException("الوظيفة المختارة لا تتبع القسم المحدد.");
    }

    private static EmployeeDto Map(Employee e) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        FullNameAr = e.FullNameAr,
        FullNameEn = e.FullNameEn,
        NationalId = e.NationalId,
        BirthDate = e.BirthDate,
        Gender = e.Gender,
        MaritalStatus = e.MaritalStatus,
        HireDate = e.HireDate,
        TerminationDate = e.TerminationDate,
        EmploymentStatus = e.EmploymentStatus,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.NameAr ?? string.Empty,
        PositionId = e.PositionId,
        PositionName = e.Position?.TitleAr ?? string.Empty,
        BasicSalary = e.BasicSalary,
        Email = e.Email,
        Phone = e.Phone,
        Address = e.Address,
        BankAccountNumber = e.BankAccountNumber,
        Iban = e.Iban,
        ApplicationUserId = e.ApplicationUserId
    };
}

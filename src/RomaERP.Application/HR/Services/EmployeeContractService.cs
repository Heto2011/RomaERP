using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class EmployeeContractService : IEmployeeContractService
{
    private readonly IApplicationDbContext _context;

    public EmployeeContractService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<EmployeeContractDto>> GetAllAsync(CancellationToken ct = default)
    {
        var contracts = await _context.EmployeeContracts
            .AsNoTracking()
            .Include(c => c.Employee)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

        return contracts.Select(c => Map(c)).ToList();
    }

    public async Task<List<EmployeeContractDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var contracts = await _context.EmployeeContracts
            .AsNoTracking()
            .Include(c => c.Employee)
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

        return contracts.Select(c => Map(c)).ToList();
    }

    public async Task<EmployeeContractDto> CreateAsync(CreateEmployeeContractDto dto, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), dto.EmployeeId);

        if (dto.ContractType == ContractType.Permanent)
        {
            if (dto.EndDate is not null)
                throw new ValidationAppException("العقد الدائم مايكونش له تاريخ انتهاء.");
        }
        else if (dto.EndDate is null || dto.EndDate <= dto.StartDate)
        {
            throw new ValidationAppException("العقد المحدد المدة/الجزئي لازم يكون له تاريخ انتهاء بعد تاريخ البداية.");
        }

        // A new contract supersedes whatever was Active for this employee — kept as history, not deleted.
        var previouslyActive = await _context.EmployeeContracts
            .Where(c => c.EmployeeId == dto.EmployeeId && c.Status == EmployeeContractStatus.Active)
            .ToListAsync(ct);
        foreach (var previous in previouslyActive)
            previous.Status = EmployeeContractStatus.Renewed;

        var contract = new EmployeeContract
        {
            EmployeeId = dto.EmployeeId,
            ContractType = dto.ContractType,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate?.Date,
            Notes = dto.Notes,
            Status = EmployeeContractStatus.Active
        };

        _context.EmployeeContracts.Add(contract);
        await _context.SaveChangesAsync(ct);

        return Map(contract, employee);
    }

    public async Task<EmployeeContractDto> UpdateStatusAsync(Guid id, UpdateEmployeeContractStatusDto dto, CancellationToken ct = default)
    {
        var contract = await _context.EmployeeContracts.Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(EmployeeContract), id);

        contract.Status = dto.Status;
        await _context.SaveChangesAsync(ct);

        return Map(contract, contract.Employee);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var contract = await _context.EmployeeContracts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(EmployeeContract), id);

        contract.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<EmployeeContractDto>> GetExpiringAsync(int days, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(days);
        var contracts = await _context.EmployeeContracts
            .AsNoTracking()
            .Include(c => c.Employee)
            .Where(c => c.Status == EmployeeContractStatus.Active && c.EndDate != null && c.EndDate <= cutoff)
            .OrderBy(c => c.EndDate)
            .ToListAsync(ct);

        return contracts.Select(c => Map(c)).ToList();
    }

    private static EmployeeContractDto Map(EmployeeContract c, Employee? employee = null) => new()
    {
        Id = c.Id,
        EmployeeId = c.EmployeeId,
        EmployeeName = (employee ?? c.Employee)?.FullNameAr ?? string.Empty,
        ContractType = c.ContractType,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        Status = c.Status,
        Notes = c.Notes,
        DaysUntilExpiry = c.EndDate is { } end ? (end.Date - DateTime.UtcNow.Date).Days : null
    };
}

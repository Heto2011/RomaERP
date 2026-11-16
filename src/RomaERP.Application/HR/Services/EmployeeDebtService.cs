using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public interface IEmployeeDebtService
{
    Task<List<EmployeeDebtDto>> GetAllAsync(Guid? employeeId, CancellationToken ct = default);
    Task<EmployeeDebtDto> CreateAdvanceAsync(CreateAdvanceDto dto, string? userName, CancellationToken ct = default);
    /// <summary>Records a debt whose journal entry was already posted elsewhere (a stock shortage charged to a person).</summary>
    Task<EmployeeDebt> AddRecordedDebtAsync(Guid employeeId, decimal amount, DateTime date, EmployeeDebtReason reason, string? description, string? reference, Guid? journalEntryId, string? userName, CancellationToken ct = default);
    Task<EmployeeDebtDto> UpdateAsync(Guid id, UpdateDebtDto dto, CancellationToken ct = default);
    Task<List<DebtDeductionProposalDto>> ProposeForRunAsync(Guid payrollRunId, CancellationToken ct = default);
    Task ApproveForRunAsync(Guid payrollRunId, ApproveDeductionsDto dto, string? userName, CancellationToken ct = default);
}

public class EmployeeDebtService : IEmployeeDebtService
{
    private readonly IApplicationDbContext _context;
    public EmployeeDebtService(IApplicationDbContext context) => _context = context;

    public async Task<List<EmployeeDebtDto>> GetAllAsync(Guid? employeeId, CancellationToken ct = default)
    {
        var q = _context.EmployeeDebts.AsNoTracking().Include(d => d.Employee).Where(d => !d.IsDeleted);
        if (employeeId is { } e) q = q.Where(d => d.EmployeeId == e);
        return (await q.OrderBy(d => d.Status).ThenByDescending(d => d.DebtDate).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<EmployeeDebtDto> CreateAdvanceAsync(CreateAdvanceDto dto, string? userName, CancellationToken ct = default)
    {
        if (dto.Amount <= 0) throw new ValidationAppException("مبلغ السلفة لازم يكون أكبر من صفر.");
        if (dto.MonthlyInstallment is <= 0) throw new ValidationAppException("القسط الشهري لازم يكون أكبر من صفر.");
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), dto.EmployeeId);
        var date = (dto.DebtDate == default ? DateTime.UtcNow : dto.DebtDate).Date;
        var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct)
            ?? throw new ValidationAppException($"مفيش فترة محاسبية بتغطي تاريخ {date:yyyy-MM-dd}.");
        if (period.IsClosed) throw new ValidationAppException($"الفترة المحاسبية ({period.Name}) مقفولة.");

        var sourceCode = string.Equals(dto.PaidFrom, "bank", StringComparison.OrdinalIgnoreCase) ? AccountingConstants.BankAccountCode : AccountingConstants.CashOnHandAccountCode;
        var custody = await AccountAsync(AccountingConstants.EmployeeCustodyAccountCode, ct);
        var source = await AccountAsync(sourceCode, ct);
        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, date, period.Id, $"سلفة موظف - {employee.FullNameAr}", custody.Id, source.Id, Math.Round(dto.Amount, 2), "EMPLOYEE-ADVANCE", ct);

        var debt = new EmployeeDebt
        {
            EmployeeId = employee.Id, Reason = EmployeeDebtReason.Advance, Description = dto.Description?.Trim(), DebtDate = date,
            Amount = Math.Round(dto.Amount, 2), RemainingAmount = Math.Round(dto.Amount, 2), MonthlyInstallment = dto.MonthlyInstallment,
            JournalEntryId = entry.Id, CreatedByName = userName
        };
        _context.EmployeeDebts.Add(debt);
        await _context.SaveChangesAsync(ct);
        var result = Map(debt);
        result.EmployeeName = employee.FullNameAr;
        return result;
    }

    public async Task<EmployeeDebt> AddRecordedDebtAsync(Guid employeeId, decimal amount, DateTime date, EmployeeDebtReason reason, string? description, string? reference, Guid? journalEntryId, string? userName, CancellationToken ct = default)
    {
        var debt = new EmployeeDebt
        {
            EmployeeId = employeeId, Reason = reason, Description = description, DebtDate = date.Date, Amount = Math.Round(amount, 2), RemainingAmount = Math.Round(amount, 2),
            SourceReference = reference, JournalEntryId = journalEntryId, CreatedByName = userName
        };
        _context.EmployeeDebts.Add(debt);
        return debt;   // saved by the caller together with the entry it belongs to
    }

    public async Task<EmployeeDebtDto> UpdateAsync(Guid id, UpdateDebtDto dto, CancellationToken ct = default)
    {
        var debt = await _context.EmployeeDebts.Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(EmployeeDebt), id);
        if (debt.Status != EmployeeDebtStatus.Active) throw new ValidationAppException("المديونية دي مش نشطة.");
        if (dto.Cancel)
        {
            // An unposted payroll deduction would leave pay short of a debt that no longer exists.
            if (await _context.EmployeeDebtDeductions.AnyAsync(x => x.EmployeeDebtId == id && !x.IsPosted, ct))
                throw new ValidationAppException("في خصم معتمد على دورة رواتب لسه متقفلتش. الغي الخصم الأول.");
            debt.Status = EmployeeDebtStatus.Cancelled;
        }
        else
        {
            if (dto.MonthlyInstallment is <= 0) throw new ValidationAppException("القسط الشهري لازم يكون أكبر من صفر.");
            debt.MonthlyInstallment = dto.MonthlyInstallment;
        }
        await _context.SaveChangesAsync(ct);
        return Map(debt);
    }

    public async Task<List<DebtDeductionProposalDto>> ProposeForRunAsync(Guid payrollRunId, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == payrollRunId, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), payrollRunId);
        var lines = await _context.PayrollRunLines.AsNoTracking().Include(l => l.Employee).Where(l => l.PayrollRunId == payrollRunId).ToListAsync(ct);
        var employeeIds = lines.Select(l => l.EmployeeId).ToList();
        var debts = await _context.EmployeeDebts.AsNoTracking().Where(d => !d.IsDeleted && d.Status == EmployeeDebtStatus.Active && employeeIds.Contains(d.EmployeeId)).OrderBy(d => d.DebtDate).ToListAsync(ct);
        var approved = await _context.EmployeeDebtDeductions.AsNoTracking().Where(d => d.PayrollRunId == payrollRunId).ToListAsync(ct);

        var result = new List<DebtDeductionProposalDto>();
        foreach (var line in lines)
        {
            // pay available to deduct from = net before any repayment already approved for this run
            var available = line.NetSalary + line.DebtDeductionAmount;
            foreach (var debt in debts.Where(d => d.EmployeeId == line.EmployeeId))
            {
                var wanted = Math.Min(debt.RemainingAmount, debt.MonthlyInstallment ?? debt.RemainingAmount);
                var proposed = Math.Max(0, Math.Min(wanted, available));
                available -= proposed;
                result.Add(new DebtDeductionProposalDto
                {
                    DebtId = debt.Id, EmployeeId = line.EmployeeId, EmployeeName = line.Employee?.FullNameAr ?? string.Empty, Description = debt.Description,
                    RemainingAmount = debt.RemainingAmount, NetSalaryBefore = line.NetSalary + line.DebtDeductionAmount, ProposedAmount = proposed,
                    ApprovedAmount = approved.Where(a => a.EmployeeDebtId == debt.Id).Sum(a => a.Amount)
                });
            }
        }
        return result;
    }

    /// <summary>The accountant approves (with any edits) what comes off each employee's pay. It replaces earlier approvals for the run, so it can be redone until the run is approved.</summary>
    public async Task ApproveForRunAsync(Guid payrollRunId, ApproveDeductionsDto dto, string? userName, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == payrollRunId, ct) ?? throw new NotFoundException(nameof(PayrollRun), payrollRunId);
        if (run.Status != PayrollRunStatus.Draft) throw new ValidationAppException("خصم المديونيات بيتعدل في دورة المسودة بس.");
        var lines = await _context.PayrollRunLines.Where(l => l.PayrollRunId == payrollRunId).ToListAsync(ct);

        // undo what was approved before for this run
        foreach (var l in lines) { l.NetSalary += l.DebtDeductionAmount; l.DebtDeductionAmount = 0; }
        var old = await _context.EmployeeDebtDeductions.Where(d => d.PayrollRunId == payrollRunId && !d.IsPosted).ToListAsync(ct);
        _context.EmployeeDebtDeductions.RemoveRange(old);

        var debtIds = dto.Items.Select(i => i.DebtId).ToList();
        var debts = await _context.EmployeeDebts.Where(d => debtIds.Contains(d.Id) && !d.IsDeleted).ToListAsync(ct);
        foreach (var item in dto.Items.Where(i => i.Amount != 0))
        {
            if (item.Amount < 0) throw new ValidationAppException("مبلغ الخصم مش ممكن يكون بالسالب.");
            var debt = debts.FirstOrDefault(d => d.Id == item.DebtId) ?? throw new NotFoundException(nameof(EmployeeDebt), item.DebtId);
            if (debt.Status != EmployeeDebtStatus.Active) throw new ValidationAppException("في مديونية مش نشطة.");
            var amount = Math.Round(item.Amount, 2);
            if (amount > debt.RemainingAmount) throw new ValidationAppException($"الخصم ({amount}) أكبر من المتبقي على المديونية ({debt.RemainingAmount}).");
            var line = lines.FirstOrDefault(l => l.EmployeeId == debt.EmployeeId)
                ?? throw new ValidationAppException("الموظف مش في دورة الرواتب دي.");
            if (amount > line.NetSalary) throw new ValidationAppException("الخصم أكبر من صافي راتب الموظف في الدورة دي.");
            line.NetSalary -= amount;
            line.DebtDeductionAmount += amount;
            _context.EmployeeDebtDeductions.Add(new EmployeeDebtDeduction { EmployeeDebtId = debt.Id, PayrollRunId = payrollRunId, EmployeeId = debt.EmployeeId, Amount = amount, ApprovedBy = userName });
        }
        await _context.SaveChangesAsync(ct);
    }

    private async Task<Domain.Accounting.Account> AccountAsync(string code, CancellationToken ct)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct)
           ?? throw new ValidationAppException($"الحساب ({code}) غير موجود في دليل الحسابات.");

    private static EmployeeDebtDto Map(EmployeeDebt d) => new()
    {
        Id = d.Id, EmployeeId = d.EmployeeId, EmployeeName = d.Employee?.FullNameAr ?? string.Empty, Reason = (int)d.Reason, Description = d.Description,
        DebtDate = d.DebtDate, Amount = d.Amount, RemainingAmount = d.RemainingAmount, MonthlyInstallment = d.MonthlyInstallment, Status = (int)d.Status,
        SourceReference = d.SourceReference, CreatedByName = d.CreatedByName
    };
}

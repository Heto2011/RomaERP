using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class PayrollService : IPayrollService
{
    private readonly IApplicationDbContext _context;

    public PayrollService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PayrollRunDto>> GetAllAsync(CancellationToken ct = default)
    {
        var runs = await _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Employee)
            .OrderByDescending(r => r.RunDate)
            .ToListAsync(ct);

        return runs.Select(Map).ToList();
    }

    public async Task<PayrollRunDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), id);

        return Map(run);
    }

    public async Task<PayrollRunDto> CreateAndCalculateAsync(CreatePayrollRunDto dto, CancellationToken ct = default)
    {
        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);

        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن إنشاء دورة رواتب لفترة محاسبية مقفلة.");

        var employees = await _context.Employees
            .Include(e => e.SalaryComponents).ThenInclude(sc => sc.SalaryComponent)
            .Where(e => !e.IsDeleted && e.EmploymentStatus == EmploymentStatus.Active)
            .ToListAsync(ct);

        var run = new PayrollRun
        {
            FiscalPeriodId = dto.FiscalPeriodId,
            RunDate = dto.RunDate,
            Description = dto.Description,
            Status = PayrollRunStatus.Draft
        };

        foreach (var employee in employees)
        {
            decimal allowances = 0;
            decimal deductions = 0;

            foreach (var esc in employee.SalaryComponents)
            {
                var component = esc.SalaryComponent!;
                var amount = component.CalculationType == CalculationType.PercentageOfBasic
                    ? employee.BasicSalary * esc.Value / 100m
                    : esc.Value;

                if (component.ComponentType == SalaryComponentType.Allowance)
                    allowances += amount;
                else
                    deductions += amount;
            }

            run.Lines.Add(new PayrollRunLine
            {
                EmployeeId = employee.Id,
                BasicSalary = employee.BasicSalary,
                TotalAllowances = allowances,
                TotalDeductions = deductions,
                NetSalary = employee.BasicSalary + allowances - deductions
            });
        }

        _context.PayrollRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(run.Id, ct);
    }

    public async Task<PayrollRunDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), id);

        if (run.Status != PayrollRunStatus.Draft)
            throw new ValidationAppException("لا يمكن اعتماد دورة رواتب إلا في حالة المسودة.");

        run.Status = PayrollRunStatus.Approved;
        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<PayrollRunDto> PostAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns
            .Include(r => r.Lines).ThenInclude(l => l.Employee).ThenInclude(e => e!.SalaryComponents).ThenInclude(sc => sc.SalaryComponent)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), id);

        if (run.Status != PayrollRunStatus.Approved)
            throw new ValidationAppException("لا يمكن ترحيل دورة رواتب إلا بعد اعتمادها.");

        var salariesExpenseAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == AccountingConstants.SalariesExpenseAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب مصروف المرتبات ({AccountingConstants.SalariesExpenseAccountCode}) غير موجود في دليل الحسابات.");

        var accruedSalariesAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == AccountingConstants.AccruedSalariesPayableAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب المرتبات المستحقة ({AccountingConstants.AccruedSalariesPayableAccountCode}) غير موجود في دليل الحسابات.");

        var deductionTotals = new Dictionary<Guid, decimal>();
        decimal totalGross = 0, totalNet = 0;
        var unlinkedDeductionCodes = new HashSet<string>();

        foreach (var line in run.Lines)
        {
            totalGross += line.BasicSalary + line.TotalAllowances;
            totalNet += line.NetSalary;

            foreach (var esc in line.Employee!.SalaryComponents.Where(x => x.SalaryComponent!.ComponentType == SalaryComponentType.Deduction))
            {
                var component = esc.SalaryComponent!;
                var amount = component.CalculationType == CalculationType.PercentageOfBasic
                    ? line.BasicSalary * esc.Value / 100m
                    : esc.Value;

                if (component.LinkedAccountId is not { } linkedAccountId)
                {
                    unlinkedDeductionCodes.Add(component.Code);
                    continue;
                }

                deductionTotals[linkedAccountId] = deductionTotals.GetValueOrDefault(linkedAccountId) + amount;
            }
        }

        if (unlinkedDeductionCodes.Count > 0)
            throw new ValidationAppException($"عناصر الخصم التالية ليس لها حساب مرتبط: {string.Join(", ", unlinkedDeductionCodes)}");

        var lines = new List<JournalEntryLine>
        {
            new()
            {
                LineNumber = 1,
                AccountId = salariesExpenseAccount.Id,
                Debit = totalGross,
                Credit = 0,
                Description = $"مصروف مرتبات - دورة {run.RunDate:yyyy-MM}"
            }
        };

        var lineNumber = 2;
        foreach (var (accountId, amount) in deductionTotals)
        {
            lines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++,
                AccountId = accountId,
                Debit = 0,
                Credit = amount,
                Description = "خصومات مرتبات"
            });
        }

        lines.Add(new JournalEntryLine
        {
            LineNumber = lineNumber,
            AccountId = accruedSalariesAccount.Id,
            Debit = 0,
            Credit = totalNet,
            Description = $"صافي مرتبات مستحقة - دورة {run.RunDate:yyyy-MM}"
        });

        var entryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}";
        var journalEntry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = run.RunDate,
            FiscalPeriodId = run.FiscalPeriodId,
            Description = $"قيد ترحيل دورة رواتب {run.RunDate:yyyy-MM}",
            Status = JournalEntryStatus.Posted,
            Lines = lines
        };

        _context.JournalEntries.Add(journalEntry);
        run.JournalEntryId = journalEntry.Id;
        run.Status = PayrollRunStatus.Posted;

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static PayrollRunDto Map(PayrollRun r) => new()
    {
        Id = r.Id,
        FiscalPeriodId = r.FiscalPeriodId,
        RunDate = r.RunDate,
        Status = r.Status,
        Description = r.Description,
        JournalEntryId = r.JournalEntryId,
        Lines = r.Lines.Select(l => new PayrollRunLineDto
        {
            EmployeeId = l.EmployeeId,
            EmployeeName = l.Employee?.FullNameAr ?? string.Empty,
            BasicSalary = l.BasicSalary,
            TotalAllowances = l.TotalAllowances,
            TotalDeductions = l.TotalDeductions,
            NetSalary = l.NetSalary
        }).ToList()
    };
}

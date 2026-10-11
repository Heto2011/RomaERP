using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services.Uk;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;

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

        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var payrollDaysPerMonth = settings?.PayrollDaysPerMonth ?? 30;

        var approvedLeaves = await _context.EmployeeRequests
            .AsNoTracking()
            .Where(r => r.Type == EmployeeRequestType.Leave
                        && r.Status == EmployeeRequestStatus.Approved
                        && r.DateFrom <= period.EndDate
                        && (r.DateTo ?? r.DateFrom) >= period.StartDate)
            .ToListAsync(ct);

        // UK income tax is cumulative over the tax year (6 April to 5 April): what each employee has already earned and been
        // taxed on in earlier runs of the same year feeds this run. Earlier runs are counted whether or not they were posted yet.
        var isUk = settings?.Country == Country.UnitedKingdom;
        var (taxYearStart, taxMonth) = UkPayrollCalculator.TaxPeriodOf(dto.RunDate);
        var yearToDate = new Dictionary<Guid, UkYearToDate>();
        if (isUk)
        {
            var earlier = await _context.PayrollRunLines
                .AsNoTracking()
                .Where(l => !l.PayrollRun!.IsDeleted && l.PayrollRun.RunDate >= taxYearStart && l.PayrollRun.RunDate < dto.RunDate)
                .GroupBy(l => l.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Taxable = g.Sum(l => l.UkTaxablePay), Tax = g.Sum(l => l.UkIncomeTax) })
                .ToListAsync(ct);
            foreach (var e in earlier) yearToDate[e.EmployeeId] = new UkYearToDate(e.Taxable, e.Tax);
        }

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

            var unpaidLeaveDays = approvedLeaves
                .Where(r => r.EmployeeId == employee.Id)
                .Sum(r =>
                {
                    var from = r.DateFrom < period.StartDate ? period.StartDate : r.DateFrom;
                    var to = (r.DateTo ?? r.DateFrom) > period.EndDate ? period.EndDate : (r.DateTo ?? r.DateFrom);
                    return (to.Date - from.Date).Days + 1;
                });

            var dailyRate = payrollDaysPerMonth > 0 ? employee.BasicSalary / payrollDaysPerMonth : 0;
            var unpaidLeaveDeduction = Math.Round(unpaidLeaveDays * dailyRate, 2);
            deductions += unpaidLeaveDeduction;

            decimal gosiEmployeeAmount = 0, gosiEmployerAmount = 0;
            if (settings?.GosiEnabled == true)
            {
                if (employee.IsSaudiNational)
                {
                    gosiEmployeeAmount = Math.Round(employee.BasicSalary * settings.GosiEmployeeRatePercent / 100m, 2);
                    gosiEmployerAmount = Math.Round(employee.BasicSalary * (settings.GosiEmployerAnnuitiesRatePercent + settings.GosiEmployerHazardsRatePercent) / 100m, 2);
                    deductions += gosiEmployeeAmount;
                }
                else
                {
                    // Non-Saudi/resident employees: no Annuities branch (employee pays nothing), only the
                    // employer-paid Occupational Hazards branch, at its own (usually lower) rate.
                    gosiEmployerAmount = Math.Round(employee.BasicSalary * settings.GosiNonSaudiEmployerHazardsRatePercent / 100m, 2);
                }
            }

            UkPayrollResult? uk = null;
            if (isUk)
            {
                try
                {
                    uk = UkPayrollCalculator.Calculate(new UkPayrollInput(
                        employee.BasicSalary + allowances - unpaidLeaveDeduction,
                        string.IsNullOrWhiteSpace(employee.UkTaxCode) ? "1257L" : employee.UkTaxCode,
                        string.IsNullOrWhiteSpace(employee.UkNiCategory) ? "A" : employee.UkNiCategory,
                        employee.UkStudentLoanPlan, employee.UkPostgraduateLoan,
                        employee.UkPensionEnrolled, employee.UkPensionEmployeePercent, employee.UkPensionEmployerPercent,
                        taxMonth, yearToDate.GetValueOrDefault(employee.Id, UkYearToDate.None)));
                }
                catch (ValidationAppException ex)
                {
                    throw new ValidationAppException($"{employee.FullNameEn}: {ex.Message}");
                }
                deductions += uk.IncomeTax + uk.EmployeeNi + uk.StudentLoan + uk.PostgraduateLoan + uk.PensionEmployee;
            }

            run.Lines.Add(new PayrollRunLine
            {
                EmployeeId = employee.Id,
                BasicSalary = employee.BasicSalary,
                TotalAllowances = allowances,
                TotalDeductions = deductions,
                NetSalary = employee.BasicSalary + allowances - deductions,
                UnpaidLeaveDays = unpaidLeaveDays,
                UnpaidLeaveDeductionAmount = unpaidLeaveDeduction,
                GosiEmployeeDeductionAmount = gosiEmployeeAmount,
                GosiEmployerContributionAmount = gosiEmployerAmount,
                UkTaxablePay = uk?.TaxablePay ?? 0,
                UkIncomeTax = uk?.IncomeTax ?? 0,
                UkEmployeeNi = uk?.EmployeeNi ?? 0,
                UkEmployerNi = uk?.EmployerNi ?? 0,
                UkStudentLoan = uk?.StudentLoan ?? 0,
                UkPostgraduateLoan = uk?.PostgraduateLoan ?? 0,
                UkPensionEmployee = uk?.PensionEmployee ?? 0,
                UkPensionEmployer = uk?.PensionEmployer ?? 0
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

    public async Task<PayrollRunDto> RevertToDraftAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), id);

        if (run.Status != PayrollRunStatus.Approved)
            throw new ValidationAppException("لا يمكن التراجع عن الاعتماد إلا لدورة رواتب معتمدة ولم تُرحّل بعد.");

        run.Status = PayrollRunStatus.Draft;
        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), id);

        if (run.Status != PayrollRunStatus.Draft)
            throw new ValidationAppException("لا يمكن إلغاء دورة رواتب إلا في حالة المسودة.");

        run.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PayrollRunDto> UpdateLineAsync(Guid runId, Guid employeeId, UpdatePayrollLineDto dto, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), runId);

        if (run.Status != PayrollRunStatus.Draft)
            throw new ValidationAppException("لا يمكن تعديل بنود دورة رواتب إلا في حالة المسودة.");

        var line = await _context.PayrollRunLines.FirstOrDefaultAsync(l => l.PayrollRunId == runId && l.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException(nameof(PayrollRunLine), employeeId);

        line.TotalAllowances = dto.TotalAllowances;
        line.TotalDeductions = dto.TotalDeductions;
        line.NetSalary = line.BasicSalary + dto.TotalAllowances - dto.TotalDeductions;

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(runId, ct);
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
        decimal totalGross = 0, totalNet = 0, totalGosiEmployeeWithheld = 0, totalGosiEmployerContribution = 0;
        decimal ukHmrcWithheld = 0, ukPensionEmployee = 0, ukEmployerNi = 0, ukEmployerPension = 0;
        var unlinkedDeductionCodes = new HashSet<string>();

        foreach (var line in run.Lines)
        {
            // Net of unpaid-leave deduction: the company simply incurs less salary expense for days not
            // worked, unlike a component deduction (tax, insurance…) which is owed elsewhere and routed
            // to its own linked account below — that split is what keeps this entry balanced.
            totalGross += line.BasicSalary + line.TotalAllowances - line.UnpaidLeaveDeductionAmount;
            totalNet += line.NetSalary;
            totalGosiEmployeeWithheld += line.GosiEmployeeDeductionAmount;
            totalGosiEmployerContribution += line.GosiEmployerContributionAmount;
            ukHmrcWithheld += line.UkIncomeTax + line.UkEmployeeNi + line.UkStudentLoan + line.UkPostgraduateLoan;
            ukPensionEmployee += line.UkPensionEmployee;
            ukEmployerNi += line.UkEmployerNi;
            ukEmployerPension += line.UkPensionEmployer;

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

        var totalGosiPayable = totalGosiEmployeeWithheld + totalGosiEmployerContribution;
        if (totalGosiPayable > 0)
        {
            if (totalGosiEmployerContribution > 0)
            {
                var gosiEmployerExpenseAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Code == AccountingConstants.GosiEmployerExpenseAccountCode && !a.IsDeleted, ct)
                    ?? throw new ValidationAppException($"حساب مصروف التأمينات الاجتماعية (حصة الشركة) ({AccountingConstants.GosiEmployerExpenseAccountCode}) غير موجود في دليل الحسابات.");

                lines.Add(new JournalEntryLine
                {
                    LineNumber = lineNumber++,
                    AccountId = gosiEmployerExpenseAccount.Id,
                    Debit = totalGosiEmployerContribution,
                    Credit = 0,
                    Description = $"مصروف تأمينات اجتماعية (حصة الشركة) - دورة {run.RunDate:yyyy-MM}"
                });
            }

            var gosiPayableAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Code == AccountingConstants.GosiPayableAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب التأمينات الاجتماعية المستحقة ({AccountingConstants.GosiPayableAccountCode}) غير موجود في دليل الحسابات.");

            lines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++,
                AccountId = gosiPayableAccount.Id,
                Debit = 0,
                Credit = totalGosiPayable,
                Description = $"تأمينات اجتماعية مستحقة (حصة الموظف والشركة) - دورة {run.RunDate:yyyy-MM}"
            });
        }

        // UK payroll: what is owed to HMRC (tax, National Insurance, student loans — employee and employer) and to the pension
        // scheme, plus the employer's own National Insurance and pension costs.
        async Task AddUkAsync(string code, string description, decimal debit, decimal credit)
        {
            if (debit == 0 && credit == 0) return;
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"الحساب ({code}) غير موجود في دليل الحسابات.");
            lines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = account.Id, Debit = debit, Credit = credit, Description = $"{description} - دورة {run.RunDate:yyyy-MM}" });
        }
        await AddUkAsync(AccountingConstants.EmployerNiExpenseAccountCode, "مصروف تأمين وطني (حصة الشركة)", ukEmployerNi, 0);
        await AddUkAsync(AccountingConstants.EmployerPensionExpenseAccountCode, "مصروف معاش (حصة الشركة)", ukEmployerPension, 0);
        var hmrcOwed = ukHmrcWithheld + ukEmployerNi;
        await AddUkAsync(AccountingConstants.HmrcPayableAccountCode, "ضرائب وتأمين وطني مستحقة", hmrcOwed < 0 ? -hmrcOwed : 0, hmrcOwed > 0 ? hmrcOwed : 0);
        await AddUkAsync(AccountingConstants.PensionPayableAccountCode, "اشتراكات معاش مستحقة", 0, ukPensionEmployee + ukEmployerPension);

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

    public async Task<List<MyPayslipDto>> GetMyPayslipsAsync(Guid employeeId, CancellationToken ct = default)
    {
        var lines = await _context.PayrollRunLines
            .AsNoTracking()
            .Include(l => l.PayrollRun)
            .Where(l => l.EmployeeId == employeeId && l.PayrollRun!.Status != PayrollRunStatus.Draft)
            .OrderByDescending(l => l.PayrollRun!.RunDate)
            .ToListAsync(ct);

        return lines.Select(l => new MyPayslipDto
        {
            RunDate = l.PayrollRun!.RunDate,
            Status = l.PayrollRun.Status,
            Description = l.PayrollRun.Description,
            BasicSalary = l.BasicSalary,
            TotalAllowances = l.TotalAllowances,
            TotalDeductions = l.TotalDeductions,
            NetSalary = l.NetSalary
        }).ToList();
    }

    public async Task<string> BuildUkSummaryCsvAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), runId);

        static string Csv(string? value)
        {
            var v = value ?? string.Empty;
            return v.Contains(',') || v.Contains('"') || v.Contains('\n') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
        static string Money(decimal value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"UK payroll summary,{run.RunDate:yyyy-MM-dd},Tax year {UkPayrollCalculator.TaxYear},{run.Status}");
        sb.AppendLine("Employee,Employee code,National Insurance number,Tax code,NI category,Gross pay,Taxable pay,Income tax,Employee NI,Employer NI,Student loan,Postgraduate loan,Employee pension,Employer pension,Net pay");

        decimal gross = 0, taxable = 0, tax = 0, eeNi = 0, erNi = 0, sl = 0, pg = 0, eePension = 0, erPension = 0, net = 0;
        foreach (var l in run.Lines.OrderBy(l => l.Employee?.FullNameEn))
        {
            var grossPay = l.BasicSalary + l.TotalAllowances - l.UnpaidLeaveDeductionAmount;
            sb.AppendLine(string.Join(",",
                Csv(l.Employee?.FullNameEn), Csv(l.Employee?.EmployeeCode), Csv(l.Employee?.UkNationalInsuranceNumber),
                Csv(string.IsNullOrWhiteSpace(l.Employee?.UkTaxCode) ? "1257L" : l.Employee!.UkTaxCode),
                Csv(string.IsNullOrWhiteSpace(l.Employee?.UkNiCategory) ? "A" : l.Employee!.UkNiCategory),
                Money(grossPay), Money(l.UkTaxablePay), Money(l.UkIncomeTax), Money(l.UkEmployeeNi), Money(l.UkEmployerNi),
                Money(l.UkStudentLoan), Money(l.UkPostgraduateLoan), Money(l.UkPensionEmployee), Money(l.UkPensionEmployer), Money(l.NetSalary)));
            gross += grossPay; taxable += l.UkTaxablePay; tax += l.UkIncomeTax; eeNi += l.UkEmployeeNi; erNi += l.UkEmployerNi;
            sl += l.UkStudentLoan; pg += l.UkPostgraduateLoan; eePension += l.UkPensionEmployee; erPension += l.UkPensionEmployer; net += l.NetSalary;
        }
        sb.AppendLine(string.Join(",", "Total", "", "", "", "", Money(gross), Money(taxable), Money(tax), Money(eeNi), Money(erNi), Money(sl), Money(pg), Money(eePension), Money(erPension), Money(net)));
        sb.AppendLine();
        sb.AppendLine($"Owed to HMRC for this run (income tax + employee NI + employer NI + student loans),{Money(tax + eeNi + erNi + sl + pg)}");
        sb.AppendLine($"Owed to the pension scheme (employee + employer),{Money(eePension + erPension)}");
        sb.AppendLine("Prepared by Roma HR for your accountant. Roma HR does not submit anything to HMRC; check these figures before filing.");
        return sb.ToString();
    }

    public async Task<PayrollSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new ValidationAppException("إعدادات الشركة غير موجودة.");

        return MapSettings(settings);
    }

    public async Task<PayrollSettingsDto> UpdateSettingsAsync(PayrollSettingsDto dto, CancellationToken ct = default)
    {
        if (dto.PayrollDaysPerMonth <= 0)
            throw new ValidationAppException("عدد أيام الراتب في الشهر يجب أن يكون أكبر من صفر.");
        if (dto.GosiEmployeeRatePercent < 0 || dto.GosiEmployerAnnuitiesRatePercent < 0 || dto.GosiEmployerHazardsRatePercent < 0 || dto.GosiNonSaudiEmployerHazardsRatePercent < 0)
            throw new ValidationAppException("نسب التأمينات الاجتماعية لا يمكن أن تكون سالبة.");

        var settings = await _context.CompanySettings.FirstOrDefaultAsync(ct)
            ?? throw new ValidationAppException("إعدادات الشركة غير موجودة.");

        settings.PayrollDaysPerMonth = dto.PayrollDaysPerMonth;
        settings.GosiEnabled = dto.GosiEnabled;
        settings.GosiEmployeeRatePercent = dto.GosiEmployeeRatePercent;
        settings.GosiEmployerAnnuitiesRatePercent = dto.GosiEmployerAnnuitiesRatePercent;
        settings.GosiEmployerHazardsRatePercent = dto.GosiEmployerHazardsRatePercent;
        settings.GosiNonSaudiEmployerHazardsRatePercent = dto.GosiNonSaudiEmployerHazardsRatePercent;

        await _context.SaveChangesAsync(ct);
        return MapSettings(settings);
    }

    private static PayrollSettingsDto MapSettings(RomaERP.Domain.Tenancy.CompanySettings s) => new()
    {
        PayrollDaysPerMonth = s.PayrollDaysPerMonth,
        GosiEnabled = s.GosiEnabled,
        GosiEmployeeRatePercent = s.GosiEmployeeRatePercent,
        GosiEmployerAnnuitiesRatePercent = s.GosiEmployerAnnuitiesRatePercent,
        GosiEmployerHazardsRatePercent = s.GosiEmployerHazardsRatePercent,
        GosiNonSaudiEmployerHazardsRatePercent = s.GosiNonSaudiEmployerHazardsRatePercent,
        IsUkPayroll = s.Country == Country.UnitedKingdom,
        UkTaxYear = UkPayrollCalculator.TaxYear
    };

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
            NetSalary = l.NetSalary,
            UnpaidLeaveDays = l.UnpaidLeaveDays,
            UnpaidLeaveDeductionAmount = l.UnpaidLeaveDeductionAmount,
            GosiEmployeeDeductionAmount = l.GosiEmployeeDeductionAmount,
            GosiEmployerContributionAmount = l.GosiEmployerContributionAmount,
            UkTaxablePay = l.UkTaxablePay,
            UkIncomeTax = l.UkIncomeTax,
            UkEmployeeNi = l.UkEmployeeNi,
            UkEmployerNi = l.UkEmployerNi,
            UkStudentLoan = l.UkStudentLoan,
            UkPostgraduateLoan = l.UkPostgraduateLoan,
            UkPensionEmployee = l.UkPensionEmployee,
            UkPensionEmployer = l.UkPensionEmployer
        }).ToList()
    };
}

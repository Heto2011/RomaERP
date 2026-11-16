using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services.Countries;
using RomaERP.Application.HR.Services.Uk;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.HR.Services;

public class PayrollService : IPayrollService
{
    private readonly IApplicationDbContext _context;
    private readonly IHtmlToPdfRenderer? _pdfRenderer;

    public PayrollService(IApplicationDbContext context, IHtmlToPdfRenderer? pdfRenderer = null)
    {
        _context = context;
        _pdfRenderer = pdfRenderer;
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

        // Each country's payroll rules live in their own class; only the ones that apply to this company take part.
        var rules = PayrollCountryRules.For(settings);
        foreach (var rule in rules) await rule.PrepareAsync(_context, dto.RunDate, ct);

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

            var line = new PayrollRunLine
            {
                EmployeeId = employee.Id,
                BasicSalary = employee.BasicSalary,
                TotalAllowances = allowances,
                UnpaidLeaveDays = unpaidLeaveDays,
                UnpaidLeaveDeductionAmount = unpaidLeaveDeduction
            };
            var grossForStatutory = employee.BasicSalary + allowances - unpaidLeaveDeduction;
            foreach (var rule in rules) deductions += rule.Apply(line, employee, grossForStatutory);
            line.TotalDeductions = deductions;
            line.NetSalary = employee.BasicSalary + allowances - deductions;
            run.Lines.Add(line);
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
        line.NetSalary = line.BasicSalary + dto.TotalAllowances - dto.TotalDeductions - line.DebtDeductionAmount;

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
        decimal totalDebtRepaid = 0;
        decimal totalGross = 0, totalNet = 0;
        var unlinkedDeductionCodes = new HashSet<string>();

        foreach (var line in run.Lines)
        {
            // Net of unpaid-leave deduction: the company simply incurs less salary expense for days not
            // worked, unlike a component deduction (tax, insurance…) which is owed elsewhere and routed
            // to its own linked account below — that split is what keeps this entry balanced.
            totalGross += line.BasicSalary + line.TotalAllowances - line.UnpaidLeaveDeductionAmount;
            totalNet += line.NetSalary;
            totalDebtRepaid += line.DebtDeductionAmount;

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

        // Internal penalties taken off pay were never paid out, so they simply lower the salaries expense; every other repayment clears what the
        // employee owed (employee custody). Both come off net pay.
        var runDeductions = await _context.EmployeeDebtDeductions.AsNoTracking().Include(d => d.EmployeeDebt).Where(d => d.PayrollRunId == run.Id && !d.IsPosted).ToListAsync(ct);
        var penaltyTotal = runDeductions.Where(d => d.EmployeeDebt!.Reason == EmployeeDebtReason.InternalPenalty).Sum(d => d.Amount);
        totalDebtRepaid -= penaltyTotal;

        var lines = new List<JournalEntryLine>
        {
            new()
            {
                LineNumber = 1,
                AccountId = salariesExpenseAccount.Id,
                Debit = totalGross - penaltyTotal,
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

        // Each country adds what it owes to its authorities and the employer's own cost; the country-neutral flow knows none of it.
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var builder = new PayrollJournalBuilder(_context, $"{run.RunDate:yyyy-MM}", lines, lineNumber, ct);
        foreach (var rule in PayrollCountryRules.For(settings)) await rule.AddJournalAsync(builder, run.Lines.ToList());
        lineNumber = builder.NextLineNumber;

        if (totalDebtRepaid > 0)
        {
            var custody = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.EmployeeCustodyAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب عهد وسلف الموظفين ({AccountingConstants.EmployeeCustodyAccountCode}) غير موجود في دليل الحسابات.");
            lines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++, AccountId = custody.Id, Debit = 0, Credit = totalDebtRepaid,
                Description = $"سداد مديونيات موظفين من الراتب - دورة {run.RunDate:yyyy-MM}"
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
        // Repayments approved for this run now reduce the debts they belong to.
        var approved = await _context.EmployeeDebtDeductions.Include(d => d.EmployeeDebt).Where(d => d.PayrollRunId == run.Id && !d.IsPosted).ToListAsync(ct);
        foreach (var d in approved)
        {
            d.IsPosted = true;
            var debt = d.EmployeeDebt!;
            debt.RemainingAmount = Math.Max(0, debt.RemainingAmount - d.Amount);
            if (debt.RemainingAmount == 0) debt.Status = EmployeeDebtStatus.Settled;
        }
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
            RunId = l.PayrollRunId,
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

    public async Task<byte[]> GetPayslipPdfAsync(Guid runId, Guid employeeId, bool arabic, bool publishedOnly, CancellationToken ct = default)
    {
        var renderer = _pdfRenderer ?? throw new ValidationAppException("إنشاء ملف PDF غير متاح حاليًا.");
        var run = await _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Lines.Where(l => l.EmployeeId == employeeId)).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException(nameof(PayrollRun), runId);
        if (publishedOnly && run.Status == PayrollRunStatus.Draft)
            throw new NotFoundException(nameof(PayrollRun), runId);

        var line = run.Lines.FirstOrDefault()
            ?? throw new NotFoundException(nameof(PayrollRunLine), employeeId);
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new ValidationAppException("إعدادات الشركة غير موجودة.");

        return await renderer.RenderAsync(PayslipHtmlTemplate.Build(run, line, settings, arabic), ct);
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

        if (settings.Country == Country.Guernsey)
        {
            if (dto.GgIncomeTaxRatePercent is < 0 or > 100 || dto.GgEmployeeSocialRatePercent is < 0 or > 100 || dto.GgEmployerSocialRatePercent is < 0 or > 100
                || dto.GgPersonalAllowanceAnnual < 0 || dto.GgSocialMonthlyUpperLimit < 0)
                throw new ValidationAppException("قيم رواتب غيرنزي غير صالحة (النسب بين 0 و100 والمبالغ لا تقل عن صفر).");
            settings.GgIncomeTaxRatePercent = dto.GgIncomeTaxRatePercent;
            settings.GgPersonalAllowanceAnnual = dto.GgPersonalAllowanceAnnual;
            settings.GgEmployeeSocialRatePercent = dto.GgEmployeeSocialRatePercent;
            settings.GgEmployerSocialRatePercent = dto.GgEmployerSocialRatePercent;
            settings.GgSocialMonthlyUpperLimit = dto.GgSocialMonthlyUpperLimit;
        }

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
        IsEgyptPayroll = s.Country == Country.Egypt,
        IsGuernseyPayroll = s.Country == Country.Guernsey,
        GgIncomeTaxRatePercent = s.GgIncomeTaxRatePercent,
        GgPersonalAllowanceAnnual = s.GgPersonalAllowanceAnnual,
        GgEmployeeSocialRatePercent = s.GgEmployeeSocialRatePercent,
        GgEmployerSocialRatePercent = s.GgEmployerSocialRatePercent,
        GgSocialMonthlyUpperLimit = s.GgSocialMonthlyUpperLimit,
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
            UkPensionEmployer = l.UkPensionEmployer,
            EgEmployeeInsurance = l.EgEmployeeInsurance,
            EgEmployerInsurance = l.EgEmployerInsurance,
            EgIncomeTax = l.EgIncomeTax,
            GgIncomeTax = l.GgIncomeTax,
            GgEmployeeSocial = l.GgEmployeeSocial,
            GgEmployerSocial = l.GgEmployerSocial
        }).ToList()
    };
}

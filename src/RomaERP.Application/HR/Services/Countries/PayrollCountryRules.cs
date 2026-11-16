using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.Services.Uk;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.HR.Services.Countries;

/// <summary>One country's payroll rules, kept entirely inside its own class: what it withholds from the employee and what it costs the
/// employer (calculation), and what it posts to the books (journal). Nothing of one country's rules appears in another's, or in the
/// country-neutral payroll flow, so a country can be changed or added without touching the others.</summary>
public interface IPayrollCountryRule
{
    /// <summary>Loads anything the rule needs about earlier runs before the run is calculated (UK year-to-date).</summary>
    Task PrepareAsync(IApplicationDbContext context, DateTime runDate, CancellationToken ct);

    /// <summary>Sets this country's amounts on the line and returns how much it deducts from the employee's pay.</summary>
    /// <param name="grossForStatutory">Basic + allowances − unpaid leave: the pay the country's taxes and insurance are worked on.</param>
    decimal Apply(PayrollRunLine line, Employee employee, decimal grossForStatutory);

    /// <summary>The journal lines this country adds to a posted run (what is owed to the authorities and the employer's own cost).</summary>
    Task AddJournalAsync(PayrollJournalBuilder journal, IReadOnlyList<PayrollRunLine> lines);
}

/// <summary>Collects the lines of the payroll journal entry, looking each account up by code.</summary>
public class PayrollJournalBuilder
{
    private readonly IApplicationDbContext _context;
    private readonly string _period;
    private readonly CancellationToken _ct;
    public List<JournalEntryLine> Lines { get; }
    public int NextLineNumber { get; private set; }

    public PayrollJournalBuilder(IApplicationDbContext context, string period, List<JournalEntryLine> lines, int nextLineNumber, CancellationToken ct)
    {
        _context = context; _period = period; Lines = lines; NextLineNumber = nextLineNumber; _ct = ct;
    }

    /// <summary>Adds a line (nothing when both amounts are zero).</summary>
    public async Task AddAsync(string accountCode, string description, decimal debit, decimal credit)
    {
        if (debit == 0 && credit == 0) return;
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == accountCode && !a.IsDeleted, _ct)
            ?? throw new ValidationAppException($"الحساب ({accountCode}) غير موجود في دليل الحسابات.");
        Lines.Add(new JournalEntryLine { LineNumber = NextLineNumber++, AccountId = account.Id, Debit = debit, Credit = credit, Description = $"{description} - دورة {_period}" });
    }

    /// <summary>A net amount owed to an authority: a credit when positive, a debit when negative.</summary>
    public Task AddOwedAsync(string accountCode, string description, decimal owed) => AddAsync(accountCode, description, owed < 0 ? -owed : 0, owed > 0 ? owed : 0);
}

public static class PayrollCountryRules
{
    /// <summary>The rules that apply to this company, each a fresh instance for one run.</summary>
    public static List<IPayrollCountryRule> For(CompanySettings? settings)
    {
        var rules = new List<IPayrollCountryRule>();
        if (settings?.GosiEnabled == true && settings is not null) rules.Add(new SaudiGosiRule(settings));
        if (settings?.Country == Country.UnitedKingdom) rules.Add(new UnitedKingdomPayrollRule());
        if (settings?.Country == Country.Egypt) rules.Add(new EgyptPayrollRule());
        if (settings?.Country == Country.Guernsey) rules.Add(new GuernseyPayrollRule(settings));
        return rules;
    }
}

/// <summary>Saudi GOSI: Annuities for Saudi nationals (employee + employer share) and the employer-paid Occupational Hazards branch for everyone,
/// on salary up to a ceiling.</summary>
public class SaudiGosiRule : IPayrollCountryRule
{
    /// <summary>The most monthly salary GOSI contributions are charged on.</summary>
    private const decimal MaxContributoryWage = 45_000m;
    private readonly CompanySettings _settings;
    public SaudiGosiRule(CompanySettings settings) => _settings = settings;

    public Task PrepareAsync(IApplicationDbContext context, DateTime runDate, CancellationToken ct) => Task.CompletedTask;

    public decimal Apply(PayrollRunLine line, Employee employee, decimal grossForStatutory)
    {
        var gosiBase = Math.Min(employee.BasicSalary, MaxContributoryWage);
        if (employee.IsSaudiNational)
        {
            line.GosiEmployeeDeductionAmount = Math.Round(gosiBase * _settings.GosiEmployeeRatePercent / 100m, 2);
            line.GosiEmployerContributionAmount = Math.Round(gosiBase * (_settings.GosiEmployerAnnuitiesRatePercent + _settings.GosiEmployerHazardsRatePercent) / 100m, 2);
            return line.GosiEmployeeDeductionAmount;
        }
        // Non-Saudi/resident employees: no Annuities branch (employee pays nothing), only the employer-paid Occupational Hazards branch.
        line.GosiEmployerContributionAmount = Math.Round(gosiBase * _settings.GosiNonSaudiEmployerHazardsRatePercent / 100m, 2);
        return 0;
    }

    public async Task AddJournalAsync(PayrollJournalBuilder journal, IReadOnlyList<PayrollRunLine> lines)
    {
        var employer = lines.Sum(l => l.GosiEmployerContributionAmount);
        var employee = lines.Sum(l => l.GosiEmployeeDeductionAmount);
        await journal.AddAsync(AccountingConstants.GosiEmployerExpenseAccountCode, "مصروف تأمينات اجتماعية (حصة الشركة)", employer, 0);
        await journal.AddAsync(AccountingConstants.GosiPayableAccountCode, "تأمينات اجتماعية مستحقة (حصة الموظف والشركة)", 0, employee + employer);
    }
}

/// <summary>UK: PAYE income tax (cumulative over the tax year), National Insurance, student loans and workplace pension.</summary>
public class UnitedKingdomPayrollRule : IPayrollCountryRule
{
    private int _taxMonth;
    private readonly Dictionary<Guid, UkYearToDate> _yearToDate = new();

    public async Task PrepareAsync(IApplicationDbContext context, DateTime runDate, CancellationToken ct)
    {
        // Income tax is cumulative: what each employee already earned and was taxed on earlier in the same tax year (6 April to 5 April) feeds this run.
        var (taxYearStart, taxMonth) = UkPayrollCalculator.TaxPeriodOf(runDate);
        _taxMonth = taxMonth;
        var earlier = await context.PayrollRunLines.AsNoTracking()
            .Where(l => !l.PayrollRun!.IsDeleted && l.PayrollRun.RunDate >= taxYearStart && l.PayrollRun.RunDate < runDate)
            .GroupBy(l => l.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Taxable = g.Sum(l => l.UkTaxablePay), Tax = g.Sum(l => l.UkIncomeTax) })
            .ToListAsync(ct);
        foreach (var e in earlier) _yearToDate[e.EmployeeId] = new UkYearToDate(e.Taxable, e.Tax);
    }

    public decimal Apply(PayrollRunLine line, Employee employee, decimal grossForStatutory)
    {
        UkPayrollResult uk;
        try
        {
            uk = UkPayrollCalculator.Calculate(new UkPayrollInput(
                grossForStatutory,
                string.IsNullOrWhiteSpace(employee.UkTaxCode) ? "1257L" : employee.UkTaxCode,
                string.IsNullOrWhiteSpace(employee.UkNiCategory) ? "A" : employee.UkNiCategory,
                employee.UkStudentLoanPlan, employee.UkPostgraduateLoan,
                employee.UkPensionEnrolled, employee.UkPensionEmployeePercent, employee.UkPensionEmployerPercent,
                _taxMonth, _yearToDate.GetValueOrDefault(employee.Id, UkYearToDate.None)));
        }
        catch (ValidationAppException ex)
        {
            throw new ValidationAppException($"{employee.FullNameEn}: {ex.Message}");
        }
        line.UkTaxablePay = uk.TaxablePay; line.UkIncomeTax = uk.IncomeTax; line.UkEmployeeNi = uk.EmployeeNi; line.UkEmployerNi = uk.EmployerNi;
        line.UkStudentLoan = uk.StudentLoan; line.UkPostgraduateLoan = uk.PostgraduateLoan; line.UkPensionEmployee = uk.PensionEmployee; line.UkPensionEmployer = uk.PensionEmployer;
        return uk.IncomeTax + uk.EmployeeNi + uk.StudentLoan + uk.PostgraduateLoan + uk.PensionEmployee;
    }

    public async Task AddJournalAsync(PayrollJournalBuilder journal, IReadOnlyList<PayrollRunLine> lines)
    {
        var employerNi = lines.Sum(l => l.UkEmployerNi);
        var employerPension = lines.Sum(l => l.UkPensionEmployer);
        var hmrcWithheld = lines.Sum(l => l.UkIncomeTax + l.UkEmployeeNi + l.UkStudentLoan + l.UkPostgraduateLoan);
        await journal.AddAsync(AccountingConstants.EmployerNiExpenseAccountCode, "مصروف تأمين وطني (حصة الشركة)", employerNi, 0);
        await journal.AddAsync(AccountingConstants.EmployerPensionExpenseAccountCode, "مصروف معاش (حصة الشركة)", employerPension, 0);
        await journal.AddOwedAsync(AccountingConstants.HmrcPayableAccountCode, "ضرائب وتأمين وطني مستحقة", hmrcWithheld + employerNi);
        await journal.AddAsync(AccountingConstants.PensionPayableAccountCode, "اشتراكات معاش مستحقة", 0, lines.Sum(l => l.UkPensionEmployee) + employerPension);
    }
}

/// <summary>Egypt: social insurance (employee and employer shares) and salary income tax.</summary>
public class EgyptPayrollRule : IPayrollCountryRule
{
    public Task PrepareAsync(IApplicationDbContext context, DateTime runDate, CancellationToken ct) => Task.CompletedTask;

    public decimal Apply(PayrollRunLine line, Employee employee, decimal grossForStatutory)
    {
        var eg = EgyptPayrollCalculator.Calculate(employee.BasicSalary, grossForStatutory, EgyptPayrollCalculator.IsInsured(employee.Nationality));
        line.EgEmployeeInsurance = eg.EmployeeInsurance; line.EgEmployerInsurance = eg.EmployerInsurance; line.EgIncomeTax = eg.IncomeTax;
        return eg.EmployeeInsurance + eg.IncomeTax;
    }

    public async Task AddJournalAsync(PayrollJournalBuilder journal, IReadOnlyList<PayrollRunLine> lines)
    {
        var employer = lines.Sum(l => l.EgEmployerInsurance);
        await journal.AddAsync(AccountingConstants.EmployerSocialInsuranceExpenseAccountCode, "مصروف تأمينات اجتماعية (حصة الشركة)", employer, 0);
        await journal.AddAsync(AccountingConstants.SocialInsurancePayableAccountCode, "تأمينات اجتماعية مستحقة", 0, lines.Sum(l => l.EgEmployeeInsurance) + employer);
        await journal.AddOwedAsync(AccountingConstants.TaxesPayableAccountCode, "ضريبة كسب العمل مستحقة", lines.Sum(l => l.EgIncomeTax));
    }
}

/// <summary>Guernsey: income tax at the company's own rate with a personal allowance, and social security (employee and employer).</summary>
public class GuernseyPayrollRule : IPayrollCountryRule
{
    private readonly CompanySettings _settings;
    public GuernseyPayrollRule(CompanySettings settings) => _settings = settings;

    public Task PrepareAsync(IApplicationDbContext context, DateTime runDate, CancellationToken ct) => Task.CompletedTask;

    public decimal Apply(PayrollRunLine line, Employee employee, decimal grossForStatutory)
    {
        var gg = GuernseyPayrollCalculator.Calculate(grossForStatutory, _settings.GgIncomeTaxRatePercent,
            employee.GgPersonalAllowanceAnnual ?? _settings.GgPersonalAllowanceAnnual, _settings.GgEmployeeSocialRatePercent, _settings.GgEmployerSocialRatePercent, _settings.GgSocialMonthlyUpperLimit);
        line.GgIncomeTax = gg.IncomeTax; line.GgEmployeeSocial = gg.EmployeeSocial; line.GgEmployerSocial = gg.EmployerSocial;
        return gg.IncomeTax + gg.EmployeeSocial;
    }

    public async Task AddJournalAsync(PayrollJournalBuilder journal, IReadOnlyList<PayrollRunLine> lines)
    {
        var employer = lines.Sum(l => l.GgEmployerSocial);
        await journal.AddAsync(AccountingConstants.EmployerSocialInsuranceExpenseAccountCode, "مصروف تأمينات اجتماعية (حصة الشركة)", employer, 0);
        await journal.AddAsync(AccountingConstants.SocialInsurancePayableAccountCode, "تأمينات اجتماعية مستحقة", 0, lines.Sum(l => l.GgEmployeeSocial) + employer);
        await journal.AddOwedAsync(AccountingConstants.TaxesPayableAccountCode, "ضريبة كسب العمل مستحقة", lines.Sum(l => l.GgIncomeTax));
    }
}

using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class LoanService : ILoanService
{
    private readonly IApplicationDbContext _context;

    public LoanService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LoanDto>> GetAllAsync(CancellationToken ct = default)
    {
        var loans = await _context.Loans.AsNoTracking().Include(l => l.Installments)
            .OrderBy(l => l.Status).ThenByDescending(l => l.DisbursementDate).ToListAsync(ct);
        return loans.Select(Map).ToList();
    }

    public List<LoanInstallmentDto> BuildSchedule(decimal principal, decimal annualPercent, int months, DateTime firstDueDate)
    {
        var monthlyRate = annualPercent / 12m / 100m;
        decimal payment;
        if (monthlyRate == 0)
        {
            payment = Math.Round(principal / months, 2);
        }
        else
        {
            var factor = (double)monthlyRate;
            payment = Math.Round((decimal)((double)principal * factor / (1 - Math.Pow(1 + factor, -months))), 2);
        }

        var list = new List<LoanInstallmentDto>();
        var balance = principal;
        for (var n = 1; n <= months; n++)
        {
            var interest = Math.Round(balance * monthlyRate, 2);
            decimal principalPart;
            decimal total;
            if (n == months)
            {
                principalPart = balance;
                total = principalPart + interest;
            }
            else
            {
                principalPart = Math.Min(balance, payment - interest);
                total = principalPart + interest;
            }
            balance -= principalPart;
            list.Add(new LoanInstallmentDto
            {
                Number = n,
                DueDate = firstDueDate.Date.AddMonths(n - 1),
                PrincipalPart = principalPart,
                InterestPart = interest,
                Total = total
            });
        }
        return list;
    }

    public async Task<LoanDto> CreateAsync(CreateLoanDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Description)) throw new ValidationAppException("اكتب وصف للقرض.");
        if (dto.Description.Trim().Length > 200) throw new ValidationAppException("الوصف طويل (أقصى حد 200 حرف).");
        if (dto.Lender is { Length: > 200 }) throw new ValidationAppException("اسم الجهة طويل.");
        if (dto.Principal <= 0) throw new ValidationAppException("مبلغ القرض لازم يكون أكبر من صفر.");
        if (dto.AnnualInterestPercent is < 0 or > 100) throw new ValidationAppException("نسبة الفايدة لازم تكون بين 0 و100.");
        if (dto.TermMonths is < 1 or > 360) throw new ValidationAppException("عدد الأقساط لازم يكون من 1 لـ360.");
        if (dto.FirstInstallmentDate.Date < dto.DisbursementDate.Date) throw new ValidationAppException("أول قسط لا يسبق تاريخ استلام القرض.");

        var loanAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.LoanAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.LoanAccountId);
        if (loanAccount.AccountType != AccountType.Liability)
            throw new ValidationAppException("حساب القرض لازم يكون من الخصوم (مثلاً: قروض طويلة الأجل).");
        var bank = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.BankAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.BankAccountId);
        if (bank.AccountType != AccountType.Asset)
            throw new ValidationAppException("حساب الاستلام والسداد لازم يكون صندوق أو بنك.");

        var interestAccount = dto.InterestExpenseAccountId is { } interestId
            ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == interestId && !a.IsDeleted, ct) ?? throw new NotFoundException(nameof(Account), interestId)
            : await GetOrCreateInterestAccountAsync(ct);
        if (interestAccount.AccountType != AccountType.Expense)
            throw new ValidationAppException("حساب الفايدة لازم يكون من المصروفات.");

        var disbursement = dto.DisbursementDate == default ? DateTime.UtcNow.Date : dto.DisbursementDate.Date;
        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= disbursement && p.EndDate >= disbursement && !p.IsClosed, ct)
            ?? throw new ValidationAppException("مفيش فترة مالية مفتوحة لتاريخ استلام القرض.");

        var principal = Math.Round(dto.Principal, 2);
        var schedule = BuildSchedule(principal, dto.AnnualInterestPercent, dto.TermMonths, dto.FirstInstallmentDate);
        var description = dto.Description.Trim();

        // The money arrives: cash/bank up, loan liability up.
        await SimpleJournalEntryFactory.CreatePostedAsync(_context, disbursement, period.Id, "استلام قرض - " + description,
            bank.Id, loanAccount.Id, principal, AccountingConstants.LoanReference, ct);

        var loan = new Loan
        {
            Description = description,
            Lender = string.IsNullOrWhiteSpace(dto.Lender) ? null : dto.Lender.Trim(),
            Principal = principal,
            AnnualInterestPercent = dto.AnnualInterestPercent,
            TermMonths = dto.TermMonths,
            DisbursementDate = disbursement,
            FirstInstallmentDate = dto.FirstInstallmentDate.Date,
            LoanAccountId = loanAccount.Id,
            BankAccountId = bank.Id,
            InterestExpenseAccountId = interestAccount.Id,
            Status = LoanStatus.Active
        };
        foreach (var row in schedule)
            loan.Installments.Add(new LoanInstallment { Number = row.Number, DueDate = row.DueDate, PrincipalPart = row.PrincipalPart, InterestPart = row.InterestPart, Total = row.Total });
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync(ct);
        return Map(loan);
    }

    public async Task<LoanDto> PayInstallmentAsync(Guid installmentId, PayInstallmentDto dto, CancellationToken ct = default)
    {
        var installment = await _context.LoanInstallments.Include(i => i.Loan).ThenInclude(l => l!.Installments)
            .FirstOrDefaultAsync(i => i.Id == installmentId, ct)
            ?? throw new NotFoundException(nameof(LoanInstallment), installmentId);
        if (installment.IsPaid) throw new ValidationAppException("القسط ده اتسدد قبل كده.");
        var loan = installment.Loan!;

        // Installments are paid in order, so the balance and the interest stay consistent.
        if (loan.Installments.Any(i => i.Number < installment.Number && !i.IsPaid))
            throw new ValidationAppException("سدّد الأقساط اللي قبله الأول.");

        var date = dto.PaymentDate == default ? DateTime.UtcNow.Date : dto.PaymentDate.Date;
        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && !p.IsClosed, ct)
            ?? throw new ValidationAppException("مفيش فترة مالية مفتوحة لتاريخ السداد ده.");

        var entryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}";
        var text = $"سداد قسط {installment.Number} من {loan.TermMonths} - {loan.Description}";
        var lines = new List<JournalEntryLine>
        {
            new() { LineNumber = 1, AccountId = loan.LoanAccountId, Debit = installment.PrincipalPart, Credit = 0, Description = text }
        };
        if (installment.InterestPart > 0)
            lines.Add(new JournalEntryLine { LineNumber = 2, AccountId = loan.InterestExpenseAccountId, Debit = installment.InterestPart, Credit = 0, Description = "فايدة - " + text });
        lines.Add(new JournalEntryLine { LineNumber = lines.Count + 1, AccountId = loan.BankAccountId, Debit = 0, Credit = installment.Total, Description = text });

        _context.JournalEntries.Add(new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = date,
            FiscalPeriodId = period.Id,
            Description = text,
            Reference = AccountingConstants.LoanReference,
            Status = JournalEntryStatus.Posted,
            Lines = lines
        });

        installment.IsPaid = true;
        installment.PaidDate = date;
        if (loan.Installments.All(i => i.IsPaid)) loan.Status = LoanStatus.Paid;
        await _context.SaveChangesAsync(ct);
        return Map(loan);
    }

    private async Task<Account> GetOrCreateInterestAccountAsync(CancellationToken ct)
    {
        var existing = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.LoanInterestExpenseAccountCode && !a.IsDeleted, ct);
        if (existing is not null) return existing;

        var parent = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "5000" && !a.IsDeleted, ct);
        var account = new Account
        {
            Code = AccountingConstants.LoanInterestExpenseAccountCode,
            NameAr = "فوائد القروض",
            NameEn = "Loan Interest Expense",
            AccountType = AccountType.Expense,
            Nature = AccountNature.Debit,
            ParentAccountId = parent?.Id,
            Level = parent is null ? 1 : parent.Level + 1,
            IsActive = true
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(ct);
        return account;
    }

    private static LoanDto Map(Loan loan)
    {
        var ordered = loan.Installments.OrderBy(i => i.Number).ToList();
        var next = ordered.FirstOrDefault(i => !i.IsPaid);
        return new LoanDto
        {
            Id = loan.Id,
            Description = loan.Description,
            Lender = loan.Lender,
            Principal = loan.Principal,
            AnnualInterestPercent = loan.AnnualInterestPercent,
            TermMonths = loan.TermMonths,
            MonthlyPayment = ordered.FirstOrDefault()?.Total ?? 0,
            Remaining = ordered.Where(i => !i.IsPaid).Sum(i => i.PrincipalPart),
            PaidInstallments = ordered.Count(i => i.IsPaid),
            NextDueDate = next?.DueDate,
            NextDueAmount = next?.Total,
            NextIsOverdue = next is not null && next.DueDate.Date < DateTime.UtcNow.Date,
            Status = loan.Status,
            Installments = ordered.Select(i => new LoanInstallmentDto
            {
                Id = i.Id, Number = i.Number, DueDate = i.DueDate, PrincipalPart = i.PrincipalPart,
                InterestPart = i.InterestPart, Total = i.Total, IsPaid = i.IsPaid, PaidDate = i.PaidDate
            }).ToList()
        };
    }
}

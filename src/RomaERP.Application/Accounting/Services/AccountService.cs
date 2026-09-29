using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class AccountService : IAccountService
{
    private readonly IApplicationDbContext _context;

    public AccountService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AccountDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

        var dtos = accounts.ToDictionary(a => a.Id, Map);

        var roots = new List<AccountDto>();
        foreach (var account in accounts)
        {
            var dto = dtos[account.Id];
            if (account.ParentAccountId is { } parentId && dtos.TryGetValue(parentId, out var parentDto))
            {
                parentDto.Children.Add(dto);
            }
            else
            {
                roots.Add(dto);
            }
        }

        return roots;
    }

    public async Task<List<AccountDto>> GetFlatListAsync(CancellationToken ct = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Code)
            .Select(a => Map(a))
            .ToListAsync(ct);
    }

    public async Task<AccountDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Account), id);
        return Map(account);
    }

    public async Task<AccountDto> CreateAsync(CreateAccountDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new ValidationAppException("كود الحساب مطلوب.");

        var codeExists = await _context.Accounts.AnyAsync(a => a.Code == dto.Code && !a.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود الحساب '{dto.Code}' مستخدم بالفعل.");

        var level = 1;
        if (dto.ParentAccountId is { } parentId)
        {
            var parent = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == parentId, ct)
                ?? throw new NotFoundException(nameof(Account), parentId);

            if (parent.AccountType != dto.AccountType)
                throw new ValidationAppException("الحساب الفرعي يجب أن يكون من نفس نوع الحساب الأب.");

            level = parent.Level + 1;
        }

        var account = new Account
        {
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            AccountType = dto.AccountType,
            Nature = dto.Nature,
            ParentAccountId = dto.ParentAccountId,
            IsControlAccount = dto.IsControlAccount,
            Level = level,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(ct);

        return Map(account);
    }

    public async Task<AccountDto> UpdateAsync(Guid id, UpdateAccountDto dto, CancellationToken ct = default)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Account), id);

        account.NameAr = dto.NameAr.Trim();
        account.NameEn = dto.NameEn.Trim();
        account.IsControlAccount = dto.IsControlAccount;
        account.IsActive = dto.IsActive;

        await _context.SaveChangesAsync(ct);
        return Map(account);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Account), id);

        var hasChildren = await _context.Accounts.AnyAsync(a => a.ParentAccountId == id && !a.IsDeleted, ct);
        if (hasChildren)
            throw new ValidationAppException("لا يمكن حذف حساب له حسابات فرعية.");

        var hasMovements = await _context.JournalEntryLines.AnyAsync(l => l.AccountId == id, ct);
        if (hasMovements)
            throw new ValidationAppException("لا يمكن حذف حساب له حركات في القيود.");

        account.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static AccountDto Map(Account a) => new()
    {
        Id = a.Id,
        Code = a.Code,
        NameAr = a.NameAr,
        NameEn = a.NameEn,
        AccountType = a.AccountType,
        Nature = a.Nature,
        ParentAccountId = a.ParentAccountId,
        IsControlAccount = a.IsControlAccount,
        IsActive = a.IsActive,
        Level = a.Level
    };
}

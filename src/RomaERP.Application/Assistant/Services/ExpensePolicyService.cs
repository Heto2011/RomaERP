using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

public class ExpensePolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? RequireProofAbove { get; set; }
    public bool BlocksApproval { get; set; }
    public bool IsActive { get; set; } = true;
}

public interface IExpensePolicyService
{
    Task<List<ExpensePolicyDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExpensePolicyDto> SaveAsync(Guid? id, ExpensePolicyDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class ExpensePolicyService : IExpensePolicyService
{
    private readonly IApplicationDbContext _context;

    public ExpensePolicyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ExpensePolicyDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _context.ExpensePolicies.AsNoTracking().Include(p => p.Account).OrderBy(p => p.Name).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<ExpensePolicyDto> SaveAsync(Guid? id, ExpensePolicyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationAppException("اكتب اسم السياسة.");
        if (dto.MaxAmount is null && dto.RequireProofAbove is null) throw new ValidationAppException("حدّد حد أقصى للمبلغ أو مبلغ يلزم عنده إيصال.");
        if (dto.MaxAmount is <= 0 || dto.RequireProofAbove is < 0) throw new ValidationAppException("المبالغ لازم تكون أكبر من صفر.");
        if (dto.AccountId is { } accountId && !await _context.Accounts.AnyAsync(a => a.Id == accountId && !a.IsDeleted, ct))
            throw new NotFoundException("Account", accountId);

        ExpensePolicy policy;
        if (id is { } existing)
            policy = await _context.ExpensePolicies.FirstOrDefaultAsync(p => p.Id == existing, ct) ?? throw new NotFoundException(nameof(ExpensePolicy), existing);
        else
        {
            policy = new ExpensePolicy();
            _context.ExpensePolicies.Add(policy);
        }
        policy.Name = dto.Name.Trim();
        policy.AccountId = dto.AccountId;
        policy.MaxAmount = dto.MaxAmount;
        policy.RequireProofAbove = dto.RequireProofAbove;
        policy.BlocksApproval = dto.BlocksApproval;
        policy.IsActive = dto.IsActive;
        await _context.SaveChangesAsync(ct);
        var saved = await _context.ExpensePolicies.AsNoTracking().Include(p => p.Account).FirstAsync(p => p.Id == policy.Id, ct);
        return Map(saved);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await _context.ExpensePolicies.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException(nameof(ExpensePolicy), id);
        policy.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static ExpensePolicyDto Map(ExpensePolicy p) => new()
    {
        Id = p.Id, Name = p.Name, AccountId = p.AccountId, AccountName = p.Account?.NameAr, MaxAmount = p.MaxAmount,
        RequireProofAbove = p.RequireProofAbove, BlocksApproval = p.BlocksApproval, IsActive = p.IsActive
    };
}

/// <summary>Checks one captured expense against the company's active policies. Pure, so the rules can be tested without a database.</summary>
public static class ExpensePolicyRules
{
    public static List<PolicyIssueDto> Evaluate(ExpenseCapture capture, IEnumerable<ExpensePolicy> policies)
    {
        var issues = new List<PolicyIssueDto>();
        if (capture.Amount is not { } amount) return issues;

        foreach (var policy in policies.Where(p => p.IsActive && !p.IsDeleted && (p.AccountId is null || p.AccountId == capture.SuggestedAccountId)))
        {
            if (policy.MaxAmount is { } max && amount > max)
                issues.Add(new PolicyIssueDto { PolicyName = policy.Name, Blocks = policy.BlocksApproval, Message = $"المبلغ {amount:0.##} أكبر من الحد المسموح ({max:0.##})." });
            if (policy.RequireProofAbove is { } proof && amount > proof && string.IsNullOrEmpty(capture.ProofStoragePath))
                issues.Add(new PolicyIssueDto { PolicyName = policy.Name, Blocks = policy.BlocksApproval, Message = $"لازم إيصال أو إثبات للمصروفات اللي فوق {proof:0.##}." });
        }
        return issues;
    }
}

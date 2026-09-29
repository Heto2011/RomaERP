using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class AccountDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsControlAccount { get; set; }
    public bool IsActive { get; set; }
    public int Level { get; set; }
    public List<AccountDto> Children { get; set; } = new();
}

public class CreateAccountDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsControlAccount { get; set; }
}

public class UpdateAccountDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsControlAccount { get; set; }
    public bool IsActive { get; set; }
}

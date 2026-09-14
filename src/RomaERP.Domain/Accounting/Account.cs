using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public class Account : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }

    public Guid? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }
    public ICollection<Account> Children { get; set; } = new List<Account>();

    public bool IsControlAccount { get; set; }
    public bool IsActive { get; set; } = true;
    public int Level { get; set; } = 1;

    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}

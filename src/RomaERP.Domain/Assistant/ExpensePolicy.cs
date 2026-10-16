using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.Assistant;

/// <summary>A company rule applied to expenses captured through the assistant: a spending ceiling and/or a receipt requirement, for one expense account or for all of them.</summary>
public class ExpensePolicy : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>The expense account the rule covers; null means every account.</summary>
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }
    /// <summary>A single expense above this amount breaks the rule. Null = no ceiling.</summary>
    public decimal? MaxAmount { get; set; }
    /// <summary>A single expense above this amount needs an attached receipt/proof. Null = never required.</summary>
    public decimal? RequireProofAbove { get; set; }
    /// <summary>True: an expense that breaks the rule cannot be approved. False: it is only flagged to the approver.</summary>
    public bool BlocksApproval { get; set; }
    public bool IsActive { get; set; } = true;
}

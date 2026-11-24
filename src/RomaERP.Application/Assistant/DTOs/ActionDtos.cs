namespace RomaERP.Application.Assistant.DTOs;

public enum AssistantActionKind
{
    None = 0,
    SalesInvoice = 1,
    CustomerPayment = 2,
    MenuPrice = 3,
    CreateCustomer = 4,
    CreateVendor = 5,
    VendorPayment = 6,
    PurchaseInvoice = 7,
    JournalEntry = 8,
    Expense = 9,
    /// <summary>A generic reviewed form (update a record, approve something, a credit note...): the fields travel as key/value pairs.</summary>
    Form = 100,
}

public record ActionCandidateDto(Guid Id, string Name, string? Extra);

/// <summary>One editable (or read-only) field of a generic draft. Type: text, number, select, textarea, readonly, hidden.</summary>
public class ActionFieldDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? Value { get; set; }
    public bool Required { get; set; }
    public List<ActionCandidateDto> Options { get; set; } = new();
}

public class ActionJournalLineDto
{
    /// <summary>What the user said (a code or a name). The server matches it to an account; the owner can change it on the draft.</summary>
    public string? Account { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public class ActionInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    /// <summary>Purchase invoices only: the expense or stock account the line is booked to. The owner chooses it; it is never guessed.</summary>
    public Guid? AccountId { get; set; }
}

/// <summary>What the assistant understood from the owner's sentence, with the names already matched to real records where it could.
/// Nothing is booked from this: the owner reviews and edits it, and only an explicit "execute" turns it into a record.</summary>
public class ActionProposalDto
{
    public AssistantActionKind Kind { get; set; }
    /// <summary>One short sentence in the owner's language saying what the assistant understood.</summary>
    public string Summary { get; set; } = string.Empty;
    /// <summary>Set when the sentence was a question, or something is missing that the assistant must not guess.</summary>
    public string? Question { get; set; }
    public List<string> Warnings { get; set; } = new();

    // Sales invoice
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public List<ActionCandidateDto> CustomerCandidates { get; set; } = new();
    public List<ActionInvoiceLineDto> Lines { get; set; } = new();
    /// <summary>1 cash, 2 card, 3 credit (on account).</summary>
    public int PaymentTerm { get; set; } = 3;
    public string? Note { get; set; }

    // Generic form drafts
    public string? FormKind { get; set; }
    public List<ActionFieldDto> Fields { get; set; } = new();

    // Daily expense (note = description, amount, accountId, method 1 cash/2 card, fundingSource 1 company/2 custody)
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public int FundingSource { get; set; } = 1;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public List<ActionCandidateDto> EmployeeCandidates { get; set; } = new();

    // Manual journal entry (saved as a draft that still has to be posted from the journal page)
    public List<ActionJournalLineDto> JournalLines { get; set; } = new();

    // New customer or supplier
    public string? PartyName { get; set; }
    public string? PartyPhone { get; set; }
    public string? PartyEmail { get; set; }
    public string? PartyNameEn { get; set; }
    public string? PartyMobile { get; set; }
    public string? PartyTaxNumber { get; set; }
    public string? PartyCommercialRegister { get; set; }
    public string? PartyContactPerson { get; set; }
    public string? PartyAddress { get; set; }
    public string? PartyCity { get; set; }
    public int PartyPaymentTermsDays { get; set; }
    public decimal PartyCreditLimit { get; set; }
    public string? PartyBankName { get; set; }
    public string? PartyBankAccount { get; set; }
    public List<ActionCandidateDto> SimilarParties { get; set; } = new();

    // Purchase invoice (supplier is chosen like a customer, and each line needs an account)
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public List<ActionCandidateDto> VendorCandidates { get; set; } = new();
    public List<ActionCandidateDto> AccountCandidates { get; set; } = new();

    // Customer payment (also used for a payment to a supplier)
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public List<ActionCandidateDto> InvoiceCandidates { get; set; } = new();
    public decimal? OutstandingAmount { get; set; }
    public decimal? Amount { get; set; }
    /// <summary>1 cash, 2 card.</summary>
    public int Method { get; set; } = 1;

    // Menu price
    public Guid? ItemId { get; set; }
    public string? ItemName { get; set; }
    public List<ActionCandidateDto> ItemCandidates { get; set; } = new();
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
}

/// <summary>The reviewed, fully matched request the owner approves. The server checks every field again.</summary>
public class ExecuteActionRequestDto
{
    public AssistantActionKind Kind { get; set; }
    public Guid? CustomerId { get; set; }
    public List<ActionInvoiceLineDto> Lines { get; set; } = new();
    public int PaymentTerm { get; set; } = 3;
    public string? Note { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal? Amount { get; set; }
    public int Method { get; set; } = 1;
    public Guid? ItemId { get; set; }
    public decimal? NewPrice { get; set; }
    public Guid? VendorId { get; set; }
    public string? PartyName { get; set; }
    public string? PartyPhone { get; set; }
    public string? PartyEmail { get; set; }
    public string? PartyNameEn { get; set; }
    public string? PartyMobile { get; set; }
    public string? PartyTaxNumber { get; set; }
    public string? PartyCommercialRegister { get; set; }
    public string? PartyContactPerson { get; set; }
    public string? PartyAddress { get; set; }
    public string? PartyCity { get; set; }
    public int PartyPaymentTermsDays { get; set; }
    public decimal PartyCreditLimit { get; set; }
    public string? PartyBankName { get; set; }
    public string? PartyBankAccount { get; set; }
    public List<ActionJournalLineDto> JournalLines { get; set; } = new();
    public Guid? AccountId { get; set; }
    public int FundingSource { get; set; } = 1;
    public Guid? EmployeeId { get; set; }
    public string? FormKind { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new();
}

public record ExecuteActionResultDto(string Message, string? Link);

/// <summary>What the language model returned: raw words from the sentence, not yet matched to anything.</summary>
public class ActionIntent
{
    public AssistantActionKind Kind { get; set; }
    public string? Question { get; set; }
    public string? CustomerName { get; set; }
    public List<ActionInvoiceLineDto> Lines { get; set; } = new();
    public string? PaymentTerm { get; set; }
    public string? Note { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal? Amount { get; set; }
    public string? Method { get; set; }
    public string? ItemName { get; set; }
    public decimal? NewPrice { get; set; }
    public string? VendorName { get; set; }
    public string? PartyName { get; set; }
    public string? PartyPhone { get; set; }
    public string? PartyEmail { get; set; }
    public string? PartyNameEn { get; set; }
    public string? PartyMobile { get; set; }
    public string? PartyTaxNumber { get; set; }
    public string? PartyCommercialRegister { get; set; }
    public string? PartyContactPerson { get; set; }
    public string? PartyAddress { get; set; }
    public string? PartyCity { get; set; }
    public int PartyPaymentTermsDays { get; set; }
    public decimal PartyCreditLimit { get; set; }
    public string? PartyBankName { get; set; }
    public string? PartyBankAccount { get; set; }
    public List<ActionJournalLineDto> JournalLines { get; set; } = new();
    public string? AccountHint { get; set; }
    public string? FundingSource { get; set; }
    public string? EmployeeName { get; set; }
}

public record ActionRequestDto(string Text);

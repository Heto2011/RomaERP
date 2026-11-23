namespace RomaERP.Application.Assistant.DTOs;

public enum AssistantActionKind
{
    None = 0,
    SalesInvoice = 1,
    CustomerPayment = 2,
    MenuPrice = 3,
}

public record ActionCandidateDto(Guid Id, string Name, string? Extra);

public class ActionInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
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

    // Customer payment
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
}

public record ActionRequestDto(string Text);

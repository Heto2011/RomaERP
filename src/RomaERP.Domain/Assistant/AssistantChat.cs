using RomaERP.Domain.Common;

namespace RomaERP.Domain.Assistant;

/// <summary>One saved conversation with the assistant. Each person sees only their own.</summary>
public class AssistantChatSession : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>The messages as JSON: a list of { role, content }. Drafts are not kept, only the words.</summary>
    public string MessagesJson { get; set; } = "[]";
}

/// <summary>A record of something the assistant booked after the owner approved it, so there is always a trail of who asked for what and when.</summary>
public class AssistantActionLog : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

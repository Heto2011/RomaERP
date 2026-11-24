using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Agent;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

public record ChatSessionSummaryDto(Guid Id, string Title, DateTime UpdatedAtUtc);
public record ChatSessionDto(Guid Id, string Title, List<AgentMessageDto> Messages);
public record SaveChatRequestDto(Guid? Id, List<AgentMessageDto> Messages);

public interface IAssistantChatStore
{
    Task<List<ChatSessionSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<ChatSessionDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ChatSessionSummaryDto> SaveAsync(SaveChatRequestDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Each person's saved conversations. Only the words are kept (never the drafts), a person sees only their own, and the oldest are dropped past 20.</summary>
public class AssistantChatStore : IAssistantChatStore
{
    public const int MaxSessions = 20;
    public const int MaxMessages = 60;
    public const int MaxChars = 4000;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly IApplicationDbContext _c;
    private readonly ICurrentUserService _user;
    public AssistantChatStore(IApplicationDbContext c, ICurrentUserService user) { _c = c; _user = user; }

    private string Me => string.IsNullOrEmpty(_user.UserId) ? throw new ValidationAppException("مش قادر أحدد المستخدم.") : _user.UserId!;

    public async Task<List<ChatSessionSummaryDto>> ListAsync(CancellationToken ct = default)
        => (await _c.AssistantChatSessions.AsNoTracking().Where(s => s.UserId == Me).OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc).Take(MaxSessions).ToListAsync(ct))
            .Select(s => new ChatSessionSummaryDto(s.Id, s.Title, s.UpdatedAtUtc ?? s.CreatedAtUtc)).ToList();

    public async Task<ChatSessionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _c.AssistantChatSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == Me, ct) ?? throw new NotFoundException(nameof(AssistantChatSession), id);
        return new(s.Id, s.Title, JsonSerializer.Deserialize<List<AgentMessageDto>>(s.MessagesJson, Json) ?? new());
    }

    public async Task<ChatSessionSummaryDto> SaveAsync(SaveChatRequestDto request, CancellationToken ct = default)
    {
        var me = Me;
        var messages = (request.Messages ?? new()).Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(MaxMessages).Select(m => new AgentMessageDto(m.Role, m.Content.Length > MaxChars ? m.Content[..MaxChars] : m.Content)).ToList();
        if (messages.Count == 0) throw new ValidationAppException("مفيش محادثة تتحفظ.");
        var first = messages.First(m => m.Role == "user").Content.Trim();
        var title = first.Length > 60 ? first[..60] + "…" : first;
        AssistantChatSession session;
        if (request.Id is { } id)
            session = await _c.AssistantChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == me, ct) ?? throw new NotFoundException(nameof(AssistantChatSession), id);
        else { session = new AssistantChatSession { UserId = me }; _c.AssistantChatSessions.Add(session); }
        session.Title = title;
        session.MessagesJson = JsonSerializer.Serialize(messages, Json);
        session.UpdatedAtUtc = DateTime.UtcNow;
        await _c.SaveChangesAsync(ct);

        var old = await _c.AssistantChatSessions.Where(s => s.UserId == me).OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc).Skip(MaxSessions).ToListAsync(ct);
        if (old.Count > 0) { foreach (var o in old) o.IsDeleted = true; await _c.SaveChangesAsync(ct); }
        return new(session.Id, session.Title, session.UpdatedAtUtc ?? session.CreatedAtUtc);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _c.AssistantChatSessions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Me, ct) ?? throw new NotFoundException(nameof(AssistantChatSession), id);
        s.IsDeleted = true;
        await _c.SaveChangesAsync(ct);
    }
}

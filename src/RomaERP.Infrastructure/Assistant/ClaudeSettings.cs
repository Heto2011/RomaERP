namespace RomaERP.Infrastructure.Assistant;

public class ClaudeSettings
{
    public const string SectionName = "Claude";

    /// <summary>API key from console.anthropic.com. Leave empty to disable the AI assistant gracefully.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-5";

    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";
}

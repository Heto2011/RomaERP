namespace RomaERP.Infrastructure.Assistant;

/// <summary>The AI prompts are written in Arabic, so an English-language customer would otherwise get Arabic
/// replies. For English we append a directive to the system prompt; for Arabic the prompts are left exactly as
/// they were, so existing Arabic behaviour is unchanged.</summary>
public static class AiLanguage
{
    public static string ReplyDirective(bool prefersArabic) => prefersArabic
        ? string.Empty
        : "\n\nLANGUAGE: The user's interface language is English. Write every reply — including clarifying questions, "
          + "expense descriptions and explanations — in clear English, even though these instructions and some data labels "
          + "are written in Arabic. Only if the user's own message is clearly written in Arabic, answer in Arabic instead.";
}

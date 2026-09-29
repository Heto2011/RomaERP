using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Assistant;

/// <summary>Implements IBusinessQaService (see that interface for the "never free-text SQL" design note):
/// pulls this month's and last month's income statement, today's balance sheet, and this month's cost-center
/// breakdown from the report services already used by the rest of the app, hands Claude only those numbers
/// as plain text, and asks it to answer the question in Egyptian Arabic. Claude never touches the database.</summary>
public class ClaudeBusinessQaService : IBusinessQaService
{
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly ClaudeSettings _settings;
    private readonly IFinancialReportService _reports;
    private readonly IAiUsageLimiter _usageLimiter;
    private readonly IUserLanguage _language;

    public ClaudeBusinessQaService(HttpClient httpClient, IOptions<ClaudeSettings> settings, IFinancialReportService reports, IAiUsageLimiter usageLimiter, IUserLanguage language)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _reports = reports;
        _usageLimiter = usageLimiter;
        _language = language;
    }

    public async Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ValidationAppException("اكتب سؤالك الأول.");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new ValidationAppException(
                "المساعد الذكي مش مفعّل لسه — لازم تضيف مفتاح Claude API في إعدادات السيرفر (Claude:ApiKey) عشان يشتغل.");
        }

        await _usageLimiter.EnsureWithinDailyLimitAsync("BusinessQa", ct);

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthEnd = monthStart.AddDays(-1);

        var thisMonth = await _reports.GetIncomeStatementAsync(monthStart, today, ct);
        var lastMonth = await _reports.GetIncomeStatementAsync(prevMonthStart, prevMonthEnd, ct);
        var balanceSheet = await _reports.GetBalanceSheetAsync(today, ct);
        var costCenters = await _reports.GetCostCenterAnalysisAsync(monthStart, today, ct);

        var context = BuildContext(today, thisMonth, lastMonth, balanceSheet, costCenters);
        return await SendQuestionAsync(question, context, ct);
    }

    private static string BuildContext(
        DateTime today, IncomeStatementDto thisMonth, IncomeStatementDto lastMonth,
        BalanceSheetDto balanceSheet, CostCenterAnalysisDto costCenters)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"تاريخ النهاردة: {today:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine($"الشهر الحالي ({thisMonth.FromDate:yyyy-MM-dd} إلى {thisMonth.ToDate:yyyy-MM-dd}):");
        sb.AppendLine($"  إجمالي الإيرادات: {thisMonth.TotalRevenue:N2}");
        sb.AppendLine($"  إجمالي المصروفات: {thisMonth.TotalExpense:N2}");
        sb.AppendLine($"  صافي الربح: {thisMonth.NetIncome:N2}");
        foreach (var line in thisMonth.RevenueLines) sb.AppendLine($"    إيراد — {line.AccountName}: {line.Amount:N2}");
        foreach (var line in thisMonth.ExpenseLines) sb.AppendLine($"    مصروف — {line.AccountName}: {line.Amount:N2}");
        sb.AppendLine();
        sb.AppendLine($"الشهر السابق ({lastMonth.FromDate:yyyy-MM-dd} إلى {lastMonth.ToDate:yyyy-MM-dd}):");
        sb.AppendLine($"  إجمالي الإيرادات: {lastMonth.TotalRevenue:N2}");
        sb.AppendLine($"  إجمالي المصروفات: {lastMonth.TotalExpense:N2}");
        sb.AppendLine($"  صافي الربح: {lastMonth.NetIncome:N2}");
        sb.AppendLine();
        sb.AppendLine($"الميزانية العمومية حتى النهاردة:");
        sb.AppendLine($"  إجمالي الأصول: {balanceSheet.TotalAssets:N2}");
        sb.AppendLine($"  إجمالي الالتزامات: {balanceSheet.TotalLiabilities:N2}");
        sb.AppendLine($"  إجمالي حقوق الملكية: {balanceSheet.TotalEquity:N2}");
        sb.AppendLine();
        sb.AppendLine("الربحية لكل نشاط/مركز تكلفة الشهر ده:");
        foreach (var c in costCenters.CostCenters)
            sb.AppendLine($"  {c.CostCenterName}: إيرادات {c.TotalRevenue:N2}، مصروفات {c.TotalExpense:N2}، صافي {c.NetAmount:N2}");

        return sb.ToString();
    }

    private async Task<string> SendQuestionAsync(string question, string context, CancellationToken ct)
    {
        var systemPrompt = $"""
            أنت مساعد مالي بتجاوب صاحب شركة على أسئلته عن أداء شركته، بالعربي المصري، باختصار ووضوح (3-5 جمل كحد أقصى).
            دول أرقام شركته الحقيقية النهارده — جاوب بناءً عليها فقط، ومتخترعش أي رقم مش موجود فيها.
            لو السؤال مش ممكن يتجاوب من الأرقام دي، قول كده بصراحة بدل ما تخمّن.

            بيانات الشركة:
            {context}
            """;

        var requestBody = new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = 512,
            ["system"] = systemPrompt + AiLanguage.ReplyDirective(_language.PrefersArabic),
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = question } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new ValidationAppException($"تعذر الاتصال بمساعد الذكاء الاصطناعي (Claude API): {response.StatusCode} — {responseText}");

        var root = JsonNode.Parse(responseText)?.AsObject()
            ?? throw new ValidationAppException("رد غير متوقع من Claude API.");

        var textBlock = root["content"]?.AsArray()
            .FirstOrDefault(n => n?["type"]?.GetValue<string>() == "text")
            ?? throw new ValidationAppException("لم يتمكن المساعد الذكي من الرد، حاول تاني.");

        return textBlock["text"]?.GetValue<string>()?.Trim() ?? string.Empty;
    }
}

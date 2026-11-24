using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Agent;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class AssistantAgentTests
{
    /// <summary>A model that follows a script: each call returns the next prepared answer, and remembers what it was shown.</summary>
    private class ScriptedModel : IAgentModelClient
    {
        public Queue<JsonObject> Answers { get; } = new();
        public List<JsonArray> Seen { get; } = new();
        public int Calls { get; private set; }
        public Task<JsonObject> CompleteAsync(string system, JsonArray messages, JsonArray tools, CancellationToken ct)
        {
            Calls++; Seen.Add(JsonNode.Parse(messages.ToJsonString())!.AsArray());
            return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : Text("(نهاية)"));
        }
    }
    private class NoLimit : IAiUsageLimiter { public int Calls { get; private set; } public Task EnsureWithinDailyLimitAsync(string k, CancellationToken ct = default) { Calls++; return Task.CompletedTask; } }

    private static JsonObject Text(string t) => new() { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = t }) };
    private static JsonObject Use(string name, string json, string id = "t1") => new() { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_use", ["id"] = id, ["name"] = name, ["input"] = JsonNode.Parse(json) }) };

    private class FakeTool : IAgentTool
    {
        public string Name => "fake"; public string Description => "x"; public JsonObject InputSchema => new() { ["type"] = "object", ["properties"] = new JsonObject() };
        public Func<AgentToolResult> Run { get; set; } = () => new("ok");
        public Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct) => Task.FromResult(Run());
    }

    private static AgentChatRequestDto Ask(string q) => new(new() { new AgentMessageDto("user", q) });

    [Fact]
    public async Task ToolResult_GoesBackToTheModel_AndTheFinalTextIsTheReply()
    {
        var model = new ScriptedModel(); model.Answers.Enqueue(Use("fake", "{}")); model.Answers.Enqueue(Text("عندك 3 عملاء."));
        var tool = new FakeTool { Run = () => new("[{\"name\":\"منى\"}]") };
        var limiter = new NoLimit();
        var svc = new AssistantAgentService(model, new[] { tool }, limiter);
        var res = await svc.ChatAsync(Ask("كام عميل؟"));
        Assert.Equal("عندك 3 عملاء.", res.Reply);
        Assert.Equal(new[] { "fake" }, res.ToolsUsed);
        Assert.Equal(2, model.Calls);
        Assert.Equal(1, limiter.Calls);  // one request is one use of the allowance, however many lookups
        var last = model.Seen[1].Last()!["content"]!.AsArray()[0]!;
        Assert.Equal("tool_result", last["type"]!.GetValue<string>());
        Assert.Contains("منى", last["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task UnknownTool_And_ToolFailure_AreReportedToTheModel_NotThrown()
    {
        var model = new ScriptedModel(); model.Answers.Enqueue(Use("nope", "{}")); model.Answers.Enqueue(Use("fake", "{}", "t2")); model.Answers.Enqueue(Text("تمام"));
        var tool = new FakeTool { Run = () => throw new ValidationAppException("التاريخ غلط") };
        var svc = new AssistantAgentService(model, new[] { tool }, new NoLimit());
        var res = await svc.ChatAsync(Ask("x"));
        Assert.Equal("تمام", res.Reply);
        var results = model.Seen[2].Last()!["content"]!.AsArray();
        Assert.True(results[0]!["is_error"]!.GetValue<bool>());
        Assert.Contains("التاريخ غلط", results[0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task ARunawayModel_StopsAfterTheRoundLimit()
    {
        var model = new ScriptedModel();
        for (var i = 0; i < 20; i++) model.Answers.Enqueue(Use("fake", "{}", "t" + i));
        var svc = new AssistantAgentService(model, new[] { new FakeTool() }, new NoLimit());
        var res = await svc.ChatAsync(Ask("x"));
        Assert.Equal(AssistantAgentService.MaxRounds, model.Calls);
        Assert.Contains("خطوات كتير", res.Reply);
    }

    [Fact]
    public async Task OversizedToolOutput_IsCutBeforeItReachesTheModel()
    {
        var model = new ScriptedModel(); model.Answers.Enqueue(Use("fake", "{}")); model.Answers.Enqueue(Text("x"));
        var svc = new AssistantAgentService(model, new[] { new FakeTool { Run = () => new(new string('a', 50_000)) } }, new NoLimit());
        await svc.ChatAsync(Ask("x"));
        var content = model.Seen[1].Last()!["content"]!.AsArray()[0]!["content"]!.GetValue<string>();
        Assert.True(content.Length < AssistantAgentService.MaxToolResultChars + 100);
    }

    [Fact]
    public async Task History_IsTrimmed_AndMustEndWithTheUser()
    {
        var model = new ScriptedModel(); model.Answers.Enqueue(Text("ok"));
        var msgs = Enumerable.Range(0, 40).Select(i => new AgentMessageDto(i % 2 == 0 ? "user" : "assistant", "m" + i)).ToList();
        msgs.Add(new AgentMessageDto("user", "الأخير"));
        var svc = new AssistantAgentService(model, new IAgentTool[0], new NoLimit());
        await svc.ChatAsync(new AgentChatRequestDto(msgs));
        Assert.Equal(AssistantAgentService.MaxHistory, model.Seen[0].Count);
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.ChatAsync(new AgentChatRequestDto(new() { new AgentMessageDto("assistant", "x") })));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.ChatAsync(new AgentChatRequestDto(new())));
    }

    [Fact]
    public async Task ADraftFromAPrepareTool_ComesBackToTheUser_AndNothingIsBooked()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        ctx.Customers.Add(new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona" });
        await ctx.SaveChangesAsync();
        var actions = new AssistantActionService(new NoParser(), ctx, null!);
        var tool = new PrepareSalesInvoiceTool(actions);
        var model = new ScriptedModel();
        model.Answers.Enqueue(Use("prepare_sales_invoice", """{"customer_name":"منى","lines":[{"description":"اشتراك","quantity":2,"unit_price":500}],"payment_term":"credit"}"""));
        model.Answers.Enqueue(Text("جهّزت مسودة، راجعها ودوس نفّذ."));
        var svc = new AssistantAgentService(model, new IAgentTool[] { tool }, new NoLimit());
        var res = await svc.ChatAsync(Ask("اعمل فاتورة لمنى"));
        var draft = Assert.Single(res.Drafts);
        Assert.Equal(AssistantActionKind.SalesInvoice, draft.Kind);
        Assert.NotNull(draft.CustomerId);
        Assert.Equal(500m, draft.Lines[0].UnitPrice);
        Assert.Empty(await ctx.SalesInvoices.ToListAsync());
        var told = model.Seen[1].Last()!["content"]!.AsArray()[0]!["content"]!.GetValue<string>();
        Assert.Contains("NOTHING was booked", told);
    }

    private class NoParser : IClaudeActionParser { public Task<ActionIntent> ParseAsync(string t, CancellationToken ct = default) => throw new NotSupportedException(); }

    [Fact]
    public async Task Tools_ReadTheBooks_Compactly()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cat = new ItemCategory { Code = "C", NameAr = "ت", NameEn = "C" };
        ctx.ItemCategories.Add(cat);
        ctx.Items.Add(new Item { Code = "I1", NameAr = "سكر", NameEn = "Sugar", UnitOfMeasure = "كجم", ItemCategoryId = cat.Id, QuantityOnHand = 2, ReorderLevel = 10 });
        ctx.Items.Add(new Item { Code = "I2", NameAr = "ملح", NameEn = "Salt", UnitOfMeasure = "كجم", ItemCategoryId = cat.Id, QuantityOnHand = 50, ReorderLevel = 10 });
        var mona = new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona" };
        ctx.Customers.Add(mona);
        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = "Y", StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        ctx.FiscalYears.Add(year);
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = today.AddDays(-10), EndDate = today.AddDays(10) };
        ctx.FiscalPeriods.Add(period);
        ctx.SalesInvoices.Add(new SalesInvoice { InvoiceNumber = "INV-1", CustomerId = mona.Id, FiscalPeriodId = period.Id, InvoiceDate = today, TotalAmount = 500, PaidAmount = 200 });
        ctx.SalesInvoices.Add(new SalesInvoice { InvoiceNumber = "INV-2", CustomerId = mona.Id, FiscalPeriodId = period.Id, InvoiceDate = today, TotalAmount = 100, PaidAmount = 100 });
        await ctx.SaveChangesAsync();

        var low = JsonNode.Parse((await new SearchItemsTool(ctx).RunAsync(new JsonObject { ["low_stock_only"] = true }, default)).Text)!.AsArray();
        Assert.Equal("سكر", low.Single()!["name"]!.GetValue<string>());

        var cust = JsonNode.Parse((await new SearchCustomersTool(ctx).RunAsync(new JsonObject { ["query"] = "mona" }, default)).Text)!.AsArray();
        Assert.Equal(300m, cust.Single()!["owes"]!.GetValue<decimal>());

        var inv = JsonNode.Parse((await new ListSalesInvoicesTool(ctx).RunAsync(new JsonObject { ["unpaid_only"] = true }, default)).Text)!.AsObject();
        Assert.Equal(1, inv["count"]!.GetValue<int>());
        Assert.Equal(300m, inv["outstanding"]!.GetValue<decimal>());

        await Assert.ThrowsAsync<ValidationAppException>(() => new ListSalesInvoicesTool(ctx).RunAsync(new JsonObject { ["from"] = "13/2/2026" }, default));
    }
}

using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Agent;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class AssistantBatchTests
{
    private class Who : ICurrentUserService
    {
        public Who(string id, params string[] roles) { UserId = id; Roles = roles; }
        public string? UserId { get; } public string? UserName => "u"; public IReadOnlyList<string> Roles { get; }
    }

    private static ApplicationDbContext Ctx() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AssistantFormService Forms(ApplicationDbContext c, params string[] roles) => new(c, null!, null!, null!, null!, null!, null!, null!, new Who("u1", roles));
    private static Dictionary<string, string?> A(params (string, string)[] kv) => kv.ToDictionary(x => x.Item1, x => (string?)x.Item2);

    [Fact]
    public async Task UpdateCustomer_ComesPrefilledFromTheRecord_AndTheAskedChangeIsApplied()
    {
        var c = Ctx();
        c.Customers.Add(new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona", Phone = "010", PaymentTermsDays = 30 });
        await c.SaveChangesAsync();
        var p = await Forms(c, "Accountant").PrepareAsync("update_customer", A(("customer", "منى"), ("phone", "011")));
        Assert.Equal(AssistantActionKind.Form, p.Kind);
        Assert.Equal("011", p.Fields.First(f => f.Key == "phone").Value);
        Assert.Equal("30", p.Fields.First(f => f.Key == "termsDays").Value);
        Assert.Equal("منى", p.Fields.First(f => f.Key == "nameAr").Value);
    }

    [Fact]
    public async Task UpdateCustomer_UnknownName_GivesNoRecordToEdit()
    {
        var c = Ctx();
        var p = await Forms(c, "Admin").PrepareAsync("update_customer", A(("customer", "مش موجود")));
        Assert.NotEmpty(p.Warnings);
        Assert.Null(p.Fields.First(f => f.Key == "target").Value);
    }

    [Theory]
    [InlineData("update_customer")]
    [InlineData("credit_note")]
    [InlineData("stock_transfer")]
    [InlineData("decide_expense")]
    public async Task APersonWithoutTheRole_CannotEvenPrepareIt(string kind)
    {
        var c = Ctx();
        await Assert.ThrowsAsync<ValidationAppException>(() => Forms(c, "HR").PrepareAsync(kind, A(("customer", "x"))));
    }

    [Fact]
    public async Task ExpenseApproval_IsForTheAdminOnly_AccountantIsRefused()
    {
        var c = Ctx();
        await Assert.ThrowsAsync<ValidationAppException>(() => Forms(c, "Accountant").ExecuteAsync("decide_expense", A(("target", Guid.NewGuid().ToString()))));
    }

    [Fact]
    public async Task UnknownFormKind_IsRefused()
    {
        await Assert.ThrowsAsync<ValidationAppException>(() => Forms(Ctx(), "Admin").PrepareAsync("delete_everything", A()));
        await Assert.ThrowsAsync<ValidationAppException>(() => Forms(Ctx(), "Admin").ExecuteAsync("delete_everything", A()));
    }

    [Fact]
    public async Task ChatStore_KeepsOnlyMyChats_TrimsLongOnes_AndDropsTheOldest()
    {
        var c = Ctx();
        var mine = new AssistantChatStore(c, new Who("me", "Admin"));
        var other = new AssistantChatStore(c, new Who("other", "Admin"));
        var long4 = new string('x', AssistantChatStore.MaxChars + 500);
        var saved = await mine.SaveAsync(new SaveChatRequestDto(null, new() { new("user", long4), new("assistant", "تمام") }));
        Assert.Empty(await other.ListAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => other.GetAsync(saved.Id));
        var got = await mine.GetAsync(saved.Id);
        Assert.Equal(AssistantChatStore.MaxChars, got.Messages[0].Content.Length);
        for (var i = 0; i < AssistantChatStore.MaxSessions + 3; i++) await mine.SaveAsync(new SaveChatRequestDto(null, new() { new("user", "سؤال " + i) }));
        Assert.Equal(AssistantChatStore.MaxSessions, (await mine.ListAsync()).Count);
        await Assert.ThrowsAsync<ValidationAppException>(() => mine.SaveAsync(new SaveChatRequestDto(null, new())));
    }

    [Fact]
    public async Task ChatStore_UpdatesTheSameChat_AndDeleteHidesIt()
    {
        var c = Ctx();
        var s = new AssistantChatStore(c, new Who("me", "Admin"));
        var a = await s.SaveAsync(new SaveChatRequestDto(null, new() { new("user", "أول سؤال") }));
        var b = await s.SaveAsync(new SaveChatRequestDto(a.Id, new() { new("user", "أول سؤال"), new("assistant", "رد"), new("user", "تاني") }));
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(3, (await s.GetAsync(a.Id)).Messages.Count);
        await s.DeleteAsync(a.Id);
        Assert.Empty(await s.ListAsync());
    }

    [Fact]
    public async Task Attention_CountsBankLinesThatAreNotMatched_ForFinanceOnly()
    {
        var c = Ctx();
        c.BankFeedTransactions.Add(new RomaERP.Domain.Accounting.BankFeedTransaction { AccountId = Guid.NewGuid(), TransactionDate = DateTime.UtcNow, Description = "x", Amount = -250 });
        c.BankFeedTransactions.Add(new RomaERP.Domain.Accounting.BankFeedTransaction { AccountId = Guid.NewGuid(), TransactionDate = DateTime.UtcNow, Description = "y", Amount = 100, IsMatched = true });
        await c.SaveChangesAsync();
        var item = Assert.Single(await new AssistantAttentionService(c, new Who("u", "Accountant")).GetAsync());
        Assert.Equal("bank-unmatched", item.Key); Assert.Equal(1, item.Count); Assert.Equal(250m, item.Amount);
        Assert.Empty(await new AssistantAttentionService(c, new Who("u", "HR")).GetAsync());
    }

    [Fact]
    public async Task Attention_ShowsOnlyWhatTheRoleMayOpen_AndOnlyWhenThereIsSomething()
    {
        var c = Ctx();
        Assert.Empty(await new AssistantAttentionService(c, new Who("u", "Admin")).GetAsync());
        Assert.Empty(await new AssistantAttentionService(c, new Who("u", "Cashier")).GetAsync());
    }
}

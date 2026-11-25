using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ItemBarcodeTests
{
    private static async Task<(ItemService Svc, ItemCategory Cat)> Rig()
    {
        var c = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cat = new ItemCategory { NameAr = "عام", NameEn = "General" };
        c.ItemCategories.Add(cat); await c.SaveChangesAsync();
        return (new ItemService(c), cat);
    }
    private static CreateItemDto New(Guid cat, string code, string? barcode) => new() { Code = code, NameAr = "صنف " + code, NameEn = code, UnitOfMeasure = "قطعة", ItemCategoryId = cat, Barcode = barcode };

    [Fact]
    public async Task Scan_FindsTheItemByBarcode_OrByItsCode_AndNotAnythingElse()
    {
        var (s, cat) = await Rig();
        var a = await s.CreateAsync(New(cat.Id, "A1", " 6221234567890 "));
        Assert.Equal("6221234567890", a.Barcode);
        Assert.Equal(a.Id, (await s.GetByBarcodeAsync("6221234567890")).Id);
        Assert.Equal(a.Id, (await s.GetByBarcodeAsync("A1")).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => s.GetByBarcodeAsync("999"));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.GetByBarcodeAsync("  "));
    }

    [Fact]
    public async Task Barcode_IsUnique_NoSpaces_AndLimited()
    {
        var (s, cat) = await Rig();
        await s.CreateAsync(New(cat.Id, "A1", "111"));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CreateAsync(New(cat.Id, "A2", "111")));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CreateAsync(New(cat.Id, "A3", "12 34")));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CreateAsync(New(cat.Id, "A4", new string('9', 65))));
        Assert.Null((await s.CreateAsync(New(cat.Id, "A5", "   "))).Barcode);
        Assert.Null((await s.CreateAsync(New(cat.Id, "A6", null))).Barcode);
    }

    [Fact]
    public async Task Update_KeepsTheBarcodeWhenNotSent_ChangesItWhenSent_ClearsItWhenEmpty_AndStillRefusesADuplicate()
    {
        var (s, cat) = await Rig();
        var a = await s.CreateAsync(New(cat.Id, "A1", "111"));
        var b = await s.CreateAsync(New(cat.Id, "B1", "222"));
        UpdateItemDto Up(string? bc) => new() { NameAr = "x", NameEn = "x", UnitOfMeasure = "قطعة", ItemCategoryId = cat.Id, Barcode = bc };
        Assert.Equal("111", (await s.UpdateAsync(a.Id, Up(null))).Barcode);
        Assert.Equal("333", (await s.UpdateAsync(a.Id, Up("333"))).Barcode);
        Assert.Equal("333", (await s.UpdateAsync(a.Id, Up("333"))).Barcode);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.UpdateAsync(a.Id, Up("222")));
        Assert.Null((await s.UpdateAsync(a.Id, Up(""))).Barcode);
        Assert.Equal("222", (await s.GetByIdAsync(b.Id)).Barcode);
    }
}

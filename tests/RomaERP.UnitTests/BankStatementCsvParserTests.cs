using System.Text;
using RomaERP.Application.Common;
using Xunit;

namespace RomaERP.UnitTests;

public class BankStatementCsvParserTests
{
    private static Stream ToStream(string csv) => new MemoryStream(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task ParseAsync_FallsBackToPositional_WhenNoRecognizedHeaderExists()
    {
        var csv = "2026-01-05,Coffee shop,-25.50\n2026-01-06,Client payment,1000\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        Assert.Equal(2, lines.Count);
        Assert.Equal(new DateTime(2026, 1, 5), lines[0].Date);
        Assert.Equal("Coffee shop", lines[0].Description);
        Assert.Equal(-25.50m, lines[0].Amount);
    }

    [Fact]
    public async Task ParseAsync_MapsByHeaderName_RegardlessOfColumnOrder()
    {
        // Amount is first here and Date is last — the opposite of the old fixed positional format.
        var csv = "Amount,Description,Date\n-25.50,Coffee shop,2026-01-05\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal(new DateTime(2026, 1, 5), line.Date);
        Assert.Equal("Coffee shop", line.Description);
        Assert.Equal(-25.50m, line.Amount);
    }

    [Fact]
    public async Task ParseAsync_RecognizesArabicHeaders()
    {
        var csv = "التاريخ,البيان,المبلغ\n2026-01-05,مقهى,-25.50\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal(new DateTime(2026, 1, 5), line.Date);
        Assert.Equal("مقهى", line.Description);
        Assert.Equal(-25.50m, line.Amount);
    }

    [Fact]
    public async Task ParseAsync_ResolvesSeparateDebitCreditColumns_LeavingSignResolutionToTheCaller()
    {
        var csv = "Date,Description,Debit,Credit\n2026-01-05,Withdrawal,500,\n2026-01-06,Deposit,,1000\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        Assert.Equal(2, lines.Count);
        Assert.Null(lines[0].Amount);
        Assert.Equal(500m, lines[0].Debit);
        Assert.Null(lines[0].Credit);
        Assert.Equal(1000m, lines[1].Credit);
    }

    [Fact]
    public async Task ParseAsync_HandlesSemicolonDelimiterAndParenthesizedNegatives()
    {
        var csv = "Date;Description;Amount\n05/01/2026;Refund fee;(25.50)\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal(-25.50m, line.Amount);
    }

    [Fact]
    public async Task ParseAsync_HandlesQuotedDescriptionsContainingTheDelimiter()
    {
        var csv = "Date,Description,Amount\n2026-01-05,\"Payment, ref 123\",100\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal("Payment, ref 123", line.Description);
        Assert.Equal(100m, line.Amount);
    }

    [Fact]
    public async Task ParseAsync_ConvertsArabicIndicDigitsInDatesAndAmounts()
    {
        var csv = "التاريخ,البيان,المبلغ\n٢٠٢٦-٠١-٠٥,مقهى,-٢٥.٥٠\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal(new DateTime(2026, 1, 5), line.Date);
        Assert.Equal(-25.50m, line.Amount);
    }

    [Fact]
    public async Task ParseAsync_StripsInvisibleBidiMarksAroundValues()
    {
        var csv = "Date,Description,Amount\n‏2026-01-05‏,Coffee,‏-25.50‏\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal(new DateTime(2026, 1, 5), line.Date);
        Assert.Equal(-25.50m, line.Amount);
    }

    [Fact]
    public async Task ParseAsync_StripsCurrencyCodeInlinedWithTheAmount()
    {
        var csv = "Date;Description;Amount\n2026-01-05;Coffee;SAR 1,500.00\n";
        var lines = await BankStatementCsvParser.ParseAsync(ToStream(csv));

        var line = Assert.Single(lines);
        Assert.Equal(1500.00m, line.Amount);
    }
}

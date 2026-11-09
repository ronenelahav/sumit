using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Tests.Application;

public class TransactionNormalizerTests
{
    private static readonly DateRange October2026 = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

    private static readonly RawTransactionFormat BankFormat =
        new(["dd/MM/yyyy"], ".", ",", SignConvention.PositiveIsIncoming);

    private readonly TransactionNormalizer _normalizer = new();

    [Fact]
    public void Normalize_ParsesProviderDatesAndAmounts()
    {
        RawTransaction[] raw =
        [
            new("05/10/2026", "1,250.00-", "  Transfer to account\n12-345-678901 "),
            new("12/10/2026", "₪3,500.00", "Salary"),
            new("20/10/2026", "-₪99.90", "Reference ١٢٣٤٥٦٧٨"), // Arabic-Indic digits.
        ];

        var result = _normalizer.Normalize(raw, BankFormat, October2026);

        Assert.Equal(
            [
                new Transaction(new DateOnly(2026, 10, 5), -1250.00m, "Transfer to account ****8901"),
                new Transaction(new DateOnly(2026, 10, 12), 3500.00m, "Salary"),
                new Transaction(new DateOnly(2026, 10, 20), -99.90m, "Reference ****٥٦٧٨"),
            ],
            result.Transactions);
    }

    [Fact]
    public void Normalize_CardSignConvention_MakesChargesNegativeAndRefundsPositive()
    {
        var cardFormat = BankFormat with { SignConvention = SignConvention.PositiveIsOutgoing };
        RawTransaction[] raw =
        [
            new("12/10/2026", "₪18.50", "Purchase"),
            new("13/10/2026", "-₪99.90", "Refund"),
        ];

        var result = _normalizer.Normalize(raw, cardFormat, October2026);

        Assert.Equal([-18.50m, 99.90m], result.Transactions.Select(t => t.Amount));
    }

    [Fact]
    public void Normalize_ExcludesRowsOutsideThePeriodAndReportsHowMany()
    {
        RawTransaction[] raw =
        [
            new("30/09/2026", "1.00", "Before"),
            new("01/10/2026", "2.00", "First day"),
            new("31/10/2026", "3.00", "Last day"),
            new("01/11/2026", "4.00", "After"),
        ];

        var result = _normalizer.Normalize(raw, BankFormat, October2026);

        Assert.Equal(["First day", "Last day"], result.Transactions.Select(t => t.Description));
        Assert.Equal(2, result.ExcludedOutsidePeriod);
    }

    [Fact]
    public void Normalize_MalformedRow_FailsTheWholeBatchWithoutLeakingTheValue()
    {
        RawTransaction[] raw =
        [
            new("11/10/2026", "5.00", "Valid row"),
            new("12/10/2026", "12a.00", "Malformed amount"),
        ];

        var exception = Assert.Throws<ExtractionException>(() => _normalizer.Normalize(raw, BankFormat, October2026));

        Assert.Contains("row 2 has an invalid or missing amount", exception.Message);
        Assert.DoesNotContain("12a.00", exception.Message);
    }
}

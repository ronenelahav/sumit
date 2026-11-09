namespace Sumit.TransactionExporter.Application;

/// <summary>How a provider writes dates and amounts. Declared by each scraper, applied by the normalizer.</summary>
public sealed record RawTransactionFormat(
    IReadOnlyList<string> DateFormats,
    string DecimalSeparator,
    string GroupSeparator,
    SignConvention SignConvention);

public enum SignConvention
{
    /// <summary>Bank-statement style: positive = money in, negative = money out.</summary>
    PositiveIsIncoming,

    /// <summary>Card-statement style: positive = charge (money out), negative = refund (money in).</summary>
    PositiveIsOutgoing,
}

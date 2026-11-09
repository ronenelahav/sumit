namespace Sumit.TransactionExporter.Application;

/// <summary>Transaction values exactly as they appear on the provider's page, before any parsing.</summary>
public sealed record RawTransaction(
    string Date,
    string Amount,
    string Description,
    RawAmountDirection Direction = RawAmountDirection.FromSign);

public sealed record RawTransactionBatch(IReadOnlyList<RawTransaction> Transactions, RawTransactionFormat Format);

public enum RawAmountDirection
{
    /// <summary>The amount text carries its own sign, interpreted with the provider's <see cref="SignConvention"/>.</summary>
    FromSign,

    /// <summary>The amount came from a debit (money out) column.</summary>
    Debit,

    /// <summary>The amount came from a credit (money in) column.</summary>
    Credit,
}

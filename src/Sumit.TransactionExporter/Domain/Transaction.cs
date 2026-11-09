namespace Sumit.TransactionExporter.Domain;

/// <summary>
/// A provider-independent transaction. Amount sign convention: negative = money out, positive = money in.
/// </summary>
public sealed record Transaction(DateOnly Date, decimal Amount, string Description);

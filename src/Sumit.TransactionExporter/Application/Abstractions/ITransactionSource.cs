using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Application.Abstractions;

public interface ITransactionSource
{
    /// <summary>
    /// Signs in to the provider and returns its transactions for the period as raw page values.
    /// Throws <see cref="ExtractionException"/> on expected failures; returns an empty batch only when
    /// the provider explicitly reported that there are no transactions.
    /// </summary>
    Task<RawTransactionBatch> FetchAsync(
        FinancialProvider provider,
        DateRange period,
        IProgress<ExtractionStage> progress,
        CancellationToken cancellationToken);
}

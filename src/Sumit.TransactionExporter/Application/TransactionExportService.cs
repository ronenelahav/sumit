using Sumit.TransactionExporter.Application.Abstractions;
using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Application;

public sealed record ExtractionResult(
    FinancialProvider Provider,
    DateRange Period,
    IReadOnlyList<Transaction> Transactions,
    int ExcludedOutsidePeriod);

/// <summary>The use cases the UI needs: list providers, extract a period's transactions, export them.</summary>
public sealed class TransactionExportService(
    ITransactionSource source,
    TransactionNormalizer normalizer,
    ITransactionExporter exporter,
    IEnumerable<ProviderInfo> providers,
    TimeProvider timeProvider,
    ILogger<TransactionExportService> logger)
{
    public IReadOnlyList<ProviderInfo> Providers { get; } = providers.OrderBy(p => p.Provider).ToList();

    public DateRange GetDefaultPeriod()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return DateRange.PreviousCalendarMonth(today);
    }

    // Every outcome is logged once, here. Logs carry the provider, stage, counts and the user-safe
    // ExtractionException message; never transaction values, descriptions or page content.
    public async Task<ExtractionResult> ExtractAsync(
        FinancialProvider provider,
        DateRange period,
        IProgress<ExtractionStage> progress,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Extraction started for {Provider}, period {PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}",
            provider, period.Start, period.LastDay);
        var started = timeProvider.GetTimestamp();
        var stages = new LoggedProgress(progress, provider, logger);

        try
        {
            var batch = await source.FetchAsync(provider, period, stages, cancellationToken);
            logger.LogInformation("Retrieved {RowCount} rows from {Provider}", batch.Transactions.Count, provider);

            stages.Report(ExtractionStage.ProcessingTransactions);
            var normalized = normalizer.Normalize(batch.Transactions, batch.Format, period);

            logger.LogInformation(
                "Extraction completed for {Provider}. Transactions: {Count}, outside period: {Excluded}, DurationMs: {DurationMs}",
                provider, normalized.Transactions.Count, normalized.ExcludedOutsidePeriod,
                (long)timeProvider.GetElapsedTime(started).TotalMilliseconds);
            return new ExtractionResult(provider, period, normalized.Transactions, normalized.ExcludedOutsidePeriod);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Extraction cancelled for {Provider} at stage {Stage}", provider, stages.Last);
            throw;
        }
        catch (ExtractionException ex)
        {
            logger.LogWarning(
                "Extraction failed for {Provider} at stage {Stage}: {Reason} ({ErrorType})",
                provider, stages.Last, ex.Message, ex.InnerException?.GetType().Name ?? nameof(ExtractionException));
            throw;
        }
        catch (Exception ex)
        {
            // Unexpected: type and stack only, since the message may echo page content. The stack is
            // logged without file paths, which can contain the Windows user name.
            logger.LogError(
                "Unexpected extraction failure for {Provider} at stage {Stage}: {ErrorType} {StackTrace}",
                provider, stages.Last, ex.GetType().FullName, new System.Diagnostics.StackTrace(ex, fNeedFileInfo: false));
            throw;
        }
    }

    public async Task ExportAsync(ExtractionResult result, Stream destination, CancellationToken cancellationToken)
    {
        await exporter.ExportAsync(result.Transactions, destination, cancellationToken);
        logger.LogInformation("Export file created for {Provider} with {Count} transactions", result.Provider, result.Transactions.Count);
    }

    public static string GetExportFileName(ExtractionResult result) =>
        $"transactions-{result.Provider.ToString().ToLowerInvariant()}-{result.Period.Start:yyyy-MM}.json";

    /// <summary>Passes stages on to the UI, logs each one and remembers the last for failure logs.</summary>
    private sealed class LoggedProgress(IProgress<ExtractionStage> inner, FinancialProvider provider, ILogger logger)
        : IProgress<ExtractionStage>
    {
        public ExtractionStage Last { get; private set; } = ExtractionStage.Ready;

        public void Report(ExtractionStage value)
        {
            Last = value;
            logger.LogInformation("{Provider}: {Stage}", provider, value);
            inner.Report(value);
        }
    }
}

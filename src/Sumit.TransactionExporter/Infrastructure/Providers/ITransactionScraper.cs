using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers;

public interface ITransactionScraper
{
    FinancialProvider Provider { get; }

    /// <summary>How this provider's pages write dates and amounts.</summary>
    RawTransactionFormat Format { get; }

    /// <summary>
    /// Extracts transactions from an authenticated session. Returns an empty list only when the page
    /// explicitly says there are no transactions; a missing or unexpected page must throw.
    /// </summary>
    Task<IReadOnlyList<RawTransaction>> ScrapeAsync(
        IBrowserSession session,
        DateRange period,
        CancellationToken cancellationToken);
}

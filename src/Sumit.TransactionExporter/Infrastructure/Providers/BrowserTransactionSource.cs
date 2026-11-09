using Microsoft.Playwright;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Application.Abstractions;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers;

/// <summary>Runs one provider's authenticator and scraper in a browser session that it owns.</summary>
public sealed class BrowserTransactionSource(
    ProviderRegistry registry,
    IBrowserSessionFactory sessionFactory) : ITransactionSource
{
    public async Task<RawTransactionBatch> FetchAsync(
        FinancialProvider provider,
        DateRange period,
        IProgress<ExtractionStage> progress,
        CancellationToken cancellationToken)
    {
        var strategies = registry.Resolve(provider);

        progress.Report(ExtractionStage.OpeningBrowser);
        await using var session = await OpenSessionAsync(cancellationToken);

        try
        {
            progress.Report(ExtractionStage.WaitingForAuthentication);
            await strategies.Authenticator.AuthenticateAsync(session, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(ExtractionStage.Authenticated);

            progress.Report(ExtractionStage.RetrievingTransactions);
            var transactions = await strategies.Scraper.ScrapeAsync(session, period, cancellationToken);

            return new RawTransactionBatch(transactions, strategies.Scraper.Format);
        }
        catch (Exception ex) when (ex is PlaywrightException or System.TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            // Playwright messages can contain URLs and page text, so they are not passed on; the
            // exception is kept as the inner exception and only its type is logged.
            var message = session.Page.IsClosed
                ? "The browser window was closed before the transactions were retrieved."
                : "The provider's website did not respond as expected, so no transactions were retrieved. " +
                  "Please try again; if the problem persists, the website may have changed.";
            throw new ExtractionException(message, ex);
        }
    }

    private async Task<IBrowserSession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await sessionFactory.CreateAsync(cancellationToken);
        }
        catch (PlaywrightException ex)
        {
            throw new ExtractionException(
                "Could not start the browser session. Run the app with --check-browser to see why.", ex);
        }
    }
}

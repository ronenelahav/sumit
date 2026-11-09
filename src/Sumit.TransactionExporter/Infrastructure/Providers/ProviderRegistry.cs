using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Infrastructure.Providers;

public sealed record ProviderStrategies(IProviderAuthenticator Authenticator, ITransactionScraper Scraper);

/// <summary>Matches the registered authenticator and scraper for a provider. There is no fallback.</summary>
public sealed class ProviderRegistry(
    IEnumerable<IProviderAuthenticator> authenticators,
    IEnumerable<ITransactionScraper> scrapers)
{
    public ProviderStrategies Resolve(FinancialProvider provider)
    {
        var authenticator = FindSingle(authenticators, a => a.Provider == provider, provider);
        var scraper = FindSingle(scrapers, s => s.Provider == provider, provider);

        if (authenticator is null || scraper is null)
        {
            throw new ExtractionException($"The provider \"{provider}\" is not implemented.");
        }

        return new ProviderStrategies(authenticator, scraper);
    }

    private static T? FindSingle<T>(IEnumerable<T> candidates, Func<T, bool> matches, FinancialProvider provider)
        where T : class
    {
        var found = candidates.Where(matches).ToList();
        return found.Count <= 1
            ? found.SingleOrDefault()
            : throw new InvalidOperationException($"More than one {typeof(T).Name} is registered for {provider}.");
    }
}

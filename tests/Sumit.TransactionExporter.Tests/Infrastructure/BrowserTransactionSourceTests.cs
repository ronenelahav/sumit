using Microsoft.Playwright;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;
using Sumit.TransactionExporter.Infrastructure.Providers;

namespace Sumit.TransactionExporter.Tests.Infrastructure;

public class BrowserTransactionSourceTests
{
    [Fact]
    public async Task FetchAsync_AuthenticationFailure_DisposesSessionAndDoesNotScrape()
    {
        var sessions = new FakeBrowserSessionFactory();
        var scraper = new RecordingScraper();
        var source = new BrowserTransactionSource(new ProviderRegistry([new FailingAuthenticator()], [scraper]), sessions);
        var period = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

        var exception = await Assert.ThrowsAsync<ExtractionException>(() =>
            source.FetchAsync(FinancialProvider.Isracard, period, new Progress<ExtractionStage>(), CancellationToken.None));

        Assert.Equal("Account locked.", exception.Message);
        Assert.True(sessions.Created!.Disposed);
        Assert.False(scraper.WasCalled);
    }

    private sealed class FakeBrowserSession : IBrowserSession
    {
        public bool Disposed { get; private set; }

        public IPage Page => throw new NotSupportedException("Fake sessions have no page.");

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeBrowserSessionFactory : IBrowserSessionFactory
    {
        public FakeBrowserSession? Created { get; private set; }

        public Task<IBrowserSession> CreateAsync(CancellationToken cancellationToken)
        {
            Created = new FakeBrowserSession();
            return Task.FromResult<IBrowserSession>(Created);
        }
    }

    private sealed class FailingAuthenticator : IProviderAuthenticator
    {
        public FinancialProvider Provider => FinancialProvider.Isracard;

        public Task AuthenticateAsync(IBrowserSession session, CancellationToken cancellationToken) =>
            throw new ExtractionException("Account locked.");
    }

    private sealed class RecordingScraper : ITransactionScraper
    {
        public bool WasCalled { get; private set; }

        public FinancialProvider Provider => FinancialProvider.Isracard;

        public RawTransactionFormat Format { get; } = new(["dd/MM/yyyy"], ".", ",", SignConvention.PositiveIsOutgoing);

        public Task<IReadOnlyList<RawTransaction>> ScrapeAsync(
            IBrowserSession session, DateRange period, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyList<RawTransaction>>([]);
        }
    }
}

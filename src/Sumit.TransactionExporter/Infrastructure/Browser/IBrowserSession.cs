using Microsoft.Playwright;

namespace Sumit.TransactionExporter.Infrastructure.Browser;

/// <summary>One isolated browser window for a single extraction. Disposing it closes the browser.</summary>
public interface IBrowserSession : IAsyncDisposable
{
    IPage Page { get; }
}

public interface IBrowserSessionFactory
{
    Task<IBrowserSession> CreateAsync(CancellationToken cancellationToken);
}

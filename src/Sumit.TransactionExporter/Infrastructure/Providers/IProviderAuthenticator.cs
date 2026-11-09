using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers;

public interface IProviderAuthenticator
{
    FinancialProvider Provider { get; }

    /// <summary>
    /// Opens the provider's sign-in page and returns once the user is signed in. Never handles
    /// credentials itself. Throws <see cref="Application.ExtractionException"/> for known failure states.
    /// </summary>
    Task AuthenticateAsync(IBrowserSession session, CancellationToken cancellationToken);
}

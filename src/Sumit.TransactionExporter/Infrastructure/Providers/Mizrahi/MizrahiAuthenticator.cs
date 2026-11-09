using Microsoft.Extensions.Options;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Mizrahi;

public sealed class MizrahiAuthenticator(
    IOptionsMonitor<ProviderSettings> providerSettings,
    IOptions<BrowserOptions> browserOptions) : IProviderAuthenticator
{
    public FinancialProvider Provider => FinancialProvider.Mizrahi;

    public async Task AuthenticateAsync(IBrowserSession session, CancellationToken cancellationToken)
    {
        var settings = providerSettings.Get(Provider.ToString());
        var page = session.Page;

        await ManualSignIn.OpenSignInPageAsync(page, settings.LoginUrl, settings.DisplayName);

        // After sign-in the bank moves to its online app (mto.mizrahi-tefahot.co.il/OnlineApp/), whose
        // menu links to the current-account (עובר ושב) section. The sign-in pages have no such link.
        await ManualSignIn.WaitForUserAsync(
            page.Locator(MizrahiTransactionScraper.CurrentAccountLink).First.WaitForAsync(new() { Timeout = 0 }),
            settings.DisplayName,
            browserOptions.Value.AuthenticationTimeout,
            cancellationToken);
    }
}

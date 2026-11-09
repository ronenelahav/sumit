using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Isracard;

public sealed class IsracardAuthenticator(
    IOptionsMonitor<ProviderSettings> providerSettings,
    IOptions<BrowserOptions> browserOptions,
    TimeProvider timeProvider) : IProviderAuthenticator
{
    public FinancialProvider Provider => FinancialProvider.Isracard;

    public async Task AuthenticateAsync(IBrowserSession session, CancellationToken cancellationToken)
    {
        var settings = providerSettings.Get(Provider.ToString());
        var page = session.Page;
        var api = new IsracardApi(settings.LoginUrl);

        await ManualSignIn.OpenSignInPageAsync(page, settings.LoginUrl, settings.DisplayName);

        await ManualSignIn.WaitForUserAsync(
            WaitUntilSignedInAsync(page, api),
            settings.DisplayName,
            browserOptions.Value.AuthenticationTimeout,
            cancellationToken);
    }

    // The user signs in (ID, password, SMS code, Cloudflare check) on Isracard's sign-in pages and is
    // then sent to the personal area (web.isracard.co.il/StatusPage at the time of writing). Rather
    // than depending on page markup, each time the window lands on an Isracard page outside the
    // sign-in pages, the data endpoint is asked whether the session is active.
    private async Task WaitUntilSignedInAsync(IPage page, IsracardApi api)
    {
        string? checkedUrl = null;
        while (true)
        {
            await page.WaitForURLAsync(
                url => url != checkedUrl && IsIsracardPageAfterSignIn(url),
                new() { Timeout = 0, WaitUntil = WaitUntilState.Commit });
            checkedUrl = page.Url;

            var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            if (await api.HasActiveSessionAsync(page, today))
            {
                return;
            }
        }
    }

    public static bool IsIsracardPageAfterSignIn(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("isracard.co.il", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".isracard.co.il", StringComparison.OrdinalIgnoreCase))
        && !uri.AbsolutePath.Contains("login", StringComparison.OrdinalIgnoreCase);
}

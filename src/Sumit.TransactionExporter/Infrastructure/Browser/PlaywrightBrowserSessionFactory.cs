using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Sumit.TransactionExporter.Application;

namespace Sumit.TransactionExporter.Infrastructure.Browser;

/// <summary>
/// Starts a dedicated Playwright driver and browser per session. Starting a browser per extraction
/// costs about a second, but it guarantees that sessions never share cookies, pages or state.
/// </summary>
public sealed class PlaywrightBrowserSessionFactory(
    IOptions<BrowserOptions> options,
    ILogger<PlaywrightBrowserSessionFactory> logger) : IBrowserSessionFactory
{
    private static readonly TimeSpan LaunchAttemptTimeout = TimeSpan.FromSeconds(20);

    // Playwright enforces LaunchAttemptTimeout itself and kills a browser that did not start in time.
    // This longer wait only covers a driver that stops responding.
    private static readonly TimeSpan LaunchWaitLimit = LaunchAttemptTimeout + TimeSpan.FromSeconds(10);

    public async Task<IBrowserSession> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var playwright = await Playwright.CreateAsync();
        IBrowser? browser = null;
        try
        {
            browser = await LaunchAsync(playwright, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // A new context is an in-memory profile: no cookies, tokens or storage outlive the session.
            // NoViewport lets --start-maximized control the real window size.
            var context = await browser.NewContextAsync(new()
            {
                AcceptDownloads = false,
                ViewportSize = options.Value.Headless ? null : ViewportSize.NoViewport,
            });
            context.SetDefaultTimeout((float)options.Value.ActionTimeout.TotalMilliseconds);

            var page = await context.NewPageAsync();
            if (!options.Value.Headless)
            {
                await page.BringToFrontAsync();
            }

            return new PlaywrightBrowserSession(playwright, browser, page);
        }
        catch
        {
            if (browser is not null)
            {
                await browser.DisposeAsync();
            }

            playwright.Dispose();
            throw;
        }
    }

    internal readonly record struct LaunchAttempt(string Name, string? Channel, string? ExecutablePath);

    /// <summary>
    /// Headed Windows launches use the installed Chrome first. Playwright's own Chromium is only a
    /// fallback and is the one optional download (<c>playwright.ps1 install chromium</c>).
    /// </summary>
    internal static IReadOnlyList<LaunchAttempt> HeadedAttempts()
    {
        if (!OperatingSystem.IsWindows())
        {
            return
            [
                new("playwright-chromium", null, null),
                new("chrome", "chrome", null),
                new("edge", "msedge", null),
            ];
        }

        // An installed executable is launched by path; the channel name is the fallback for a browser
        // that was not found, so the same program is not tried twice.
        var attempts = InstalledBrowser.Find()
            .Select(installed => new LaunchAttempt(installed.Name, null, installed.Path))
            .ToList();
        if (attempts.All(attempt => attempt.Name != "chrome"))
        {
            attempts.Insert(0, new("chrome", "chrome", null));
        }

        if (attempts.All(attempt => attempt.Name != "edge"))
        {
            attempts.Add(new("edge", "msedge", null));
        }

        attempts.Add(new("playwright-chromium", null, null));
        return attempts;
    }

    // Executable paths and launch errors can contain the Windows user name, so they are logged at
    // Debug only. --check-browser turns Debug on for this class.
    private async Task<IBrowser> LaunchAsync(IPlaywright playwright, CancellationToken cancellationToken)
    {
        var attempts = options.Value.Headless
            ? [new LaunchAttempt("playwright-chromium", null, null)]
            : HeadedAttempts();
        Exception? failure = null;

        foreach (var attempt in attempts)
        {
            logger.LogInformation("Opening sign-in browser: {Browser}", attempt.Name);
            logger.LogDebug("Launching {Browser} from {Location}", attempt.Name, attempt.ExecutablePath ?? attempt.Channel ?? "Playwright's Chromium");

            var launch = playwright.Chromium.LaunchAsync(new()
            {
                Headless = options.Value.Headless,
                Channel = attempt.ExecutablePath is null ? attempt.Channel : null,
                ExecutablePath = attempt.ExecutablePath,
                Args = options.Value.Headless ? null : ["--start-maximized"],
                Timeout = (float)LaunchAttemptTimeout.TotalMilliseconds,
            });
            try
            {
                var browser = await launch.WaitAsync(LaunchWaitLimit, cancellationToken);
                logger.LogInformation("Sign-in browser started: {Browser}", attempt.Name);
                return browser;
            }
            catch (Exception ex) when (ex is OperationCanceledException or System.TimeoutException)
            {
                _ = CloseIfLaunchedLateAsync(launch);
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                failure = ex;
                LogLaunchFailure(attempt, ex);
            }
            catch (PlaywrightException ex)
            {
                failure = ex;
                LogLaunchFailure(attempt, ex);
            }
        }

        throw new ExtractionException(
            $"Could not open a sign-in window (tried {string.Join(", ", attempts.Select(attempt => attempt.Name))}). " +
            "Run the app with --check-browser to see why.",
            failure);
    }

    private void LogLaunchFailure(LaunchAttempt attempt, Exception exception)
    {
        logger.LogWarning("Could not start {Browser}: {ErrorType}", attempt.Name, exception.GetType().Name);
        logger.LogDebug("Could not start {Browser}: {Reason}", attempt.Name, FirstLine(exception));
    }

    // The abandoned launch keeps running inside the driver. If it still produces a browser, close it;
    // if it fails, or the driver has been disposed in the meantime, there is nothing left to close.
    private static async Task CloseIfLaunchedLateAsync(Task<IBrowser> launch)
    {
        try
        {
            var browser = await launch.ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best effort cleanup of a launch nobody waits for any more.
        }
    }

    /// <summary>The first line of a launch error: the missing program or path, without the call log.</summary>
    internal static string FirstLine(Exception exception)
    {
        var line = exception.Message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? exception.GetType().Name;
        return line.Length <= 300 ? line : line[..300] + "...";
    }

    private sealed class PlaywrightBrowserSession(IPlaywright playwright, IBrowser browser, IPage page) : IBrowserSession
    {
        public IPage Page => page;

        public async ValueTask DisposeAsync()
        {
            try
            {
                await browser.CloseAsync();
            }
            catch (PlaywrightException)
            {
                // The browser is already gone, e.g. the user closed the window.
            }
            finally
            {
                playwright.Dispose();
            }
        }
    }
}

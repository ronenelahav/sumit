using Microsoft.Playwright;
using Sumit.TransactionExporter.Application;

namespace Sumit.TransactionExporter.Infrastructure.Browser;

/// <summary>
/// Shared steps for sign-in flows where the user types credentials and completes MFA directly on the
/// provider's page. The application only opens the page and watches for the outcome.
/// </summary>
internal static class ManualSignIn
{
    public static async Task OpenSignInPageAsync(IPage page, string url, string providerName)
    {
        try
        {
            await page.GotoAsync(url);
        }
        catch (Exception ex) when (ex is PlaywrightException or System.TimeoutException && !page.IsClosed)
        {
            throw new ExtractionException($"Could not open the {providerName} sign-in page. Check the network connection and try again.", ex);
        }
    }

    /// <summary>
    /// Waits until <paramref name="signInOutcome"/> completes. The task should be a Playwright wait
    /// without its own timeout: the timeout and cancellation are applied here, because Playwright for
    /// .NET does not accept a <see cref="CancellationToken"/>.
    /// </summary>
    public static async Task WaitForUserAsync(
        Task signInOutcome,
        string providerName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await signInOutcome.WaitAsync(timeout, cancellationToken);
        }
        // Playwright reports its own operation timeouts as System.TimeoutException too; those fault
        // signInOutcome and propagate as a website failure, not as the user running out of time.
        catch (System.TimeoutException) when (!signInOutcome.IsFaulted)
        {
            ObserveAbandoned(signInOutcome);
            throw new ExtractionException(
                $"Sign-in to {providerName} was not completed in time. Try again and finish signing in within {timeout.TotalMinutes:0.#} minutes.");
        }
        catch (OperationCanceledException)
        {
            ObserveAbandoned(signInOutcome);
            throw;
        }
    }

    // The abandoned Playwright wait faults when the session is disposed; observe it so the fault
    // is not reported as an unobserved task exception.
    private static void ObserveAbandoned(Task task) =>
        task.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
}

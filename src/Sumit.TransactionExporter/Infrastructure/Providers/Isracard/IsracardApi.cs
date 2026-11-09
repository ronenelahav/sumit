using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Isracard;

/// <summary>
/// Reads Isracard's data endpoint (<c>/services/ProxyRequestHandler.ashx</c> on the sign-in host).
/// After sign-in Isracard moves the user to web.isracard.co.il, from where the browser would block
/// requests to the sign-in host, so requests are made by navigating a separate tab of the same
/// signed-in browser to the endpoint. Request names and response fields follow the open-source
/// israeli-bank-scrapers project (eshaham/israeli-bank-scrapers, base-isracard-amex.ts) and were
/// checked against a signed-in session.
/// </summary>
internal sealed class IsracardApi(string loginUrl)
{
    private readonly Uri _servicesUrl = new(new Uri(loginUrl), "/services/ProxyRequestHandler.ashx");

    /// <summary>Opens a tab for <see cref="GetAsync"/>; the caller closes it.</summary>
    public static Task<IPage> OpenDataTabAsync(IPage signedInPage) => signedInPage.Context.NewPageAsync();

    /// <summary>Asks for the dashboard summary, which only succeeds with an active session.</summary>
    public async Task<bool> HasActiveSessionAsync(IPage signedInPage, DateOnly today)
    {
        var tab = await OpenDataTabAsync(signedInPage);
        try
        {
            var billingDate = new DateOnly(today.Year, today.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using var response = await GetAsync(tab, $"reqName=DashboardMonth&actionCode=0&billingDate={billingDate}&format=Json");
            return response is not null && IsSuccess(response.RootElement);
        }
        catch (PlaywrightException)
        {
            // A timeout (System.TimeoutException) is not caught: a check that gets no answer is a
            // website failure, and treating it as "not signed in yet" would wait out the sign-in deadline.
            return false;
        }
        finally
        {
            await tab.CloseAsync();
        }
    }

    /// <summary>Returns the parsed response, or null when the request failed or did not return JSON.</summary>
    public async Task<JsonDocument?> GetAsync(IPage dataTab, string query)
    {
        var response = await dataTab.GotoAsync($"{_servicesUrl}?{query}");
        if (response is null || !response.Ok)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(await response.TextAsync());
        }
        catch (JsonException)
        {
            // An HTML page (sign-in redirect or bot challenge) instead of data.
            return null;
        }
    }

    public static bool IsSuccess(JsonElement response) => Status(response) == "1";

    /// <summary>Isracard's own status code: "1" is success, "-2" means there is no signed-in session.</summary>
    public static string Status(JsonElement response) =>
        response.ValueKind == JsonValueKind.Object
        && response.TryGetProperty("Header", out var header)
        && header.ValueKind == JsonValueKind.Object
        && header.TryGetProperty("Status", out var status)
            ? status.ToString()
            : "missing";
}

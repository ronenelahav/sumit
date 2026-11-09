using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Mizrahi;

/// <summary>
/// Opens the current-account transactions page (עובר ושב) the way a user would, captures the request
/// the page makes for its table, and repeats it with the period's dates. The repeated request carries
/// the page's own anti-forgery header, as the site's requests do. Approach and endpoint follow
/// israeli-bank-scrapers (mizrahi.ts). Reads the account currently selected in the bank's app.
/// </summary>
public sealed class MizrahiTransactionScraper : ITransactionScraper
{
    internal const string CurrentAccountLink = "a[href*='/osh/legacy/legacy-Osh-Main']";
    private const string TransactionsLink = "a[href*='/osh/legacy/root-main-osh-p428New']";
    private const string TransactionsEndpoint = "/api/SkyOSH/get428Index";
    private const string XsrfHeader = "mizrahixsrftoken";
    private const int MaxRows = 100_000;

    private const string PostScript = """
        async ([url, body, headers]) => {
            const response = await fetch(url, { method: "POST", credentials: "include", body, headers });
            return response.ok ? await response.text() : null;
        }
        """;

    public FinancialProvider Provider => FinancialProvider.Mizrahi;

    public RawTransactionFormat Format { get; } = new(["yyyy-MM-dd"], ".", ",", SignConvention.PositiveIsIncoming);

    public async Task<IReadOnlyList<RawTransaction>> ScrapeAsync(
        IBrowserSession session,
        DateRange period,
        CancellationToken cancellationToken)
    {
        var page = session.Page;

        await page.Locator(CurrentAccountLink).First.ClickAsync();
        var pageRequest = await page.RunAndWaitForRequestAsync(
            () => page.Locator(TransactionsLink).First.ClickAsync(),
            request => request.Method == "POST" && request.Url.Contains(TransactionsEndpoint, StringComparison.OrdinalIgnoreCase));

        cancellationToken.ThrowIfCancellationRequested();

        var pageHeaders = await pageRequest.AllHeadersAsync();
        var headers = new Dictionary<string, string>
        {
            [XsrfHeader] = pageHeaders.GetValueOrDefault(XsrfHeader) ?? "",
            ["Content-Type"] = pageHeaders.GetValueOrDefault("content-type") ?? "application/json",
        };

        var text = await page.EvaluateAsync<string?>(
            PostScript,
            new object[] { pageRequest.Url, BuildRequestBody(pageRequest.PostData, period), headers });
        if (text is null)
        {
            throw new ExtractionException("Mizrahi Tefahot did not return transaction data. Please try again.");
        }

        using var response = ParseJson(text);
        return MizrahiTransactionMapper.Map(response.RootElement);
    }

    /// <summary>The page's own request body with the period's dates and a row limit large enough for a month.</summary>
    public static string BuildRequestBody(string? pageRequestBody, DateRange period)
    {
        var body = JsonNode.Parse(string.IsNullOrEmpty(pageRequestBody) ? "{}" : pageRequestBody) as JsonObject
            ?? throw new ExtractionException("Mizrahi Tefahot's transactions page sent an unexpected request.");

        body["inFromDate"] = period.Start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        body["inToDate"] = period.LastDay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        if (body["table"] is JsonObject table)
        {
            table["maxRow"] = MaxRows;
        }

        return body.ToJsonString();
    }

    private static JsonDocument ParseJson(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            throw new ExtractionException("Mizrahi Tefahot returned a page instead of transaction data. The session may have expired.");
        }
    }
}

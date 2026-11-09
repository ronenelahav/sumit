using System.Text.Json;
using Sumit.TransactionExporter.Application;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Mizrahi;

/// <summary>
/// Reads a <c>get428Index</c> (current-account transactions) response. Field names follow
/// israeli-bank-scrapers (mizrahi.ts) and were checked against a signed-in session:
/// <c>MC02PeulaTaaEZ</c> is the transaction date (<c>yyyy-MM-ddTHH:mm:ss</c>), <c>MC02SchumEZ</c> the
/// signed amount (negative = money out) and <c>MC02TnuaTeurEZ</c> the description. Rows with
/// <c>RecTypeSpecified: false</c> are not transactions (for example the balance line).
/// </summary>
public static class MizrahiTransactionMapper
{
    public static IReadOnlyList<RawTransaction> Map(JsonElement response)
    {
        if (TryGetObject(response, "header", out var header)
            && header.TryGetProperty("success", out var success)
            && success.ValueKind == JsonValueKind.False)
        {
            throw new ExtractionException("Mizrahi Tefahot refused the transaction request. Please sign in again.");
        }

        if (!TryGetObject(response, "body", out var body)
            || !TryGetObject(body, "table", out var table)
            || !table.TryGetProperty("rows", out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            throw new ExtractionException("Mizrahi Tefahot returned an unexpected response: the transaction table is missing.");
        }

        if (table.TryGetProperty("isHasMoreRows", out var hasMore) && hasMore.ValueKind == JsonValueKind.True)
        {
            throw new ExtractionException("Mizrahi Tefahot returned only part of the transactions. Please try again.");
        }

        var transactions = new List<RawTransaction>();
        foreach (var row in rows.EnumerateArray())
        {
            // A row without the flag cannot be classified; skipping it could drop a real transaction.
            if (row.ValueKind != JsonValueKind.Object
                || !row.TryGetProperty("RecTypeSpecified", out var isTransaction)
                || isTransaction.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new ExtractionException(
                    "Mizrahi Tefahot returned a transaction row in an unexpected format, so no transactions were exported.");
            }

            if (isTransaction.ValueKind == JsonValueKind.True)
            {
                transactions.Add(new RawTransaction(
                    DatePart(Text(row, "MC02PeulaTaaEZ")), Text(row, "MC02SchumEZ"), Text(row, "MC02TnuaTeurEZ")));
            }
        }

        return transactions;
    }

    private static bool TryGetObject(JsonElement parent, string property, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(property, out value)
            && value.ValueKind == JsonValueKind.Object;
    }

    private static string DatePart(string dateTime)
    {
        var separator = dateTime.IndexOf('T');
        return separator > 0 ? dateTime[..separator] : dateTime;
    }

    private static string Text(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                _ => "",
            }
            : "";
}

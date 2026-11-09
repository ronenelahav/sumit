using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sumit.TransactionExporter.Application;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Isracard;

/// <summary>
/// Turns one <c>CardsTransactionsList</c> response into raw transactions. Field meanings follow
/// israeli-bank-scrapers: domestic rows use <c>fullPurchaseDate</c>/<c>paymentSum</c>/<c>fullSupplierNameHeb</c>,
/// abroad rows (non-zero <c>dealSumOutbound</c>) use the <c>...Outbound</c> fields. Amounts are the
/// charged amount, positive for a charge.
/// </summary>
public static partial class IsracardTransactionMapper
{
    private const string IsracardDateFormat = "dd/MM/yyyy";
    private const string SummaryVoucher = "000000000";

    /// <summary>
    /// Throws when the transaction list is missing or not in the expected shape, so that a changed
    /// response is never exported as a month without transactions. A card, group or list that is
    /// absent or null means "nothing here", as israeli-bank-scrapers treats it.
    /// </summary>
    public static IReadOnlyList<RawTransaction> Map(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object
            || !response.TryGetProperty("CardsTransactionsListBean", out var cards)
            || cards.ValueKind != JsonValueKind.Object)
        {
            throw UnexpectedFormat();
        }

        var transactions = new List<RawTransaction>();
        foreach (var card in cards.EnumerateObject().Where(c => CardKey().IsMatch(c.Name)))
        {
            foreach (var group in OptionalArray(card.Value, "CurrentCardTransactions"))
            {
                foreach (var row in OptionalArray(group, "txnIsrael").Concat(OptionalArray(group, "txnAbroad")))
                {
                    if (row.ValueKind != JsonValueKind.Object)
                    {
                        throw UnexpectedFormat();
                    }

                    if (!IsSummaryRow(row))
                    {
                        transactions.Add(ToRawTransaction(row));
                    }
                }
            }
        }

        return transactions;
    }

    private static IEnumerable<JsonElement> OptionalArray(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            throw UnexpectedFormat();
        }

        if (!parent.TryGetProperty(property, out var list) || list.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return list.ValueKind == JsonValueKind.Array ? list.EnumerateArray() : throw UnexpectedFormat();
    }

    private static ExtractionException UnexpectedFormat() =>
        new("Isracard returned transaction data in an unexpected format, so no transactions were exported. The website may have changed.");

    // Isracard mixes per-card total lines into the lists; they are marked like this.
    private static bool IsSummaryRow(JsonElement row) =>
        Text(row, "dealSumType") == "1"
        || Text(row, "voucherNumberRatz") == SummaryVoucher
        || Text(row, "voucherNumberRatzOutbound") == SummaryVoucher;

    private static RawTransaction ToRawTransaction(JsonElement row)
    {
        var abroad = IsNonZero(row, "dealSumOutbound");
        var date = Text(row, abroad ? "fullPurchaseDateOutbound" : "fullPurchaseDate");
        var amount = Text(row, abroad ? "paymentSumOutbound" : "paymentSum");
        var description = Text(row, abroad ? "fullSupplierNameOutbound" : "fullSupplierNameHeb");
        var moreInfo = Text(row, "moreInfo");

        // Isracard gives every installment row the original purchase date. Installment N is allocated
        // to the month N-1 months after the purchase, so that date is used instead: the scraper reads
        // several billing months, and without the shift all installments would land in the purchase
        // month. The result is the installment's allocated month, not its posting date. This matches
        // fixInstallments in israeli-bank-scrapers.
        var installment = InstallmentNumber(moreInfo);
        if (installment > 1
            && DateOnly.TryParseExact(date, IsracardDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var purchaseDate))
        {
            date = purchaseDate.AddMonths(installment - 1).ToString(IsracardDateFormat, CultureInfo.InvariantCulture);
        }

        if (installment > 0)
        {
            description = $"{description} ({moreInfo.Trim()})";
        }

        return new RawTransaction(date, amount, description);
    }

    // "תשלום 2 מתוך 3" = installment 2 of 3. Other notes (amounts, standing orders) are not installments.
    private static int InstallmentNumber(string moreInfo)
    {
        var match = Installment().Match(moreInfo);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
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

    private static bool IsNonZero(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value)
        && value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDecimal() != 0,
            JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number != 0,
            _ => false,
        };

    [GeneratedRegex(@"^Index\d+$")]
    private static partial Regex CardKey();

    [GeneratedRegex(@"תשלום\s+(\d+)\s+מתוך\s+\d+")]
    private static partial Regex Installment();
}

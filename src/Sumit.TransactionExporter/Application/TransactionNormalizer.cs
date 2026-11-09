using System.Globalization;
using System.Text.RegularExpressions;
using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Application;

public sealed record NormalizationResult(IReadOnlyList<Transaction> Transactions, int ExcludedOutsidePeriod);

/// <summary>
/// Converts raw page values into <see cref="Transaction"/>s. Any malformed row fails the whole batch:
/// silently dropping a financial record would produce an export that looks complete but is not.
/// </summary>
public sealed partial class TransactionNormalizer
{
    private static readonly string[] CurrencyMarkers = ["₪", "$", "€", "£", "ILS", "NIS", "USD", "EUR"];

    public NormalizationResult Normalize(
        IReadOnlyList<RawTransaction> rawTransactions,
        RawTransactionFormat format,
        DateRange period)
    {
        var numberFormat = new NumberFormatInfo
        {
            NumberDecimalSeparator = format.DecimalSeparator,
            NumberGroupSeparator = format.GroupSeparator,
        };

        var dateFormats = format.DateFormats.ToArray();

        var transactions = rawTransactions
            .Select((raw, index) => NormalizeRow(raw, index + 1, dateFormats, numberFormat, format.SignConvention))
            .ToList();

        var inPeriod = transactions
            .Where(t => period.Contains(t.Date))
            .OrderBy(t => t.Date) // Stable sort: same-day rows keep the provider's order.
            .ToList();

        return new NormalizationResult(inPeriod, transactions.Count - inPeriod.Count);
    }

    private static Transaction NormalizeRow(
        RawTransaction raw,
        int rowNumber,
        string[] dateFormats,
        NumberFormatInfo numberFormat,
        SignConvention signConvention)
    {
        if (!DateOnly.TryParseExact(
                raw.Date.Trim(),
                dateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw InvalidRow(rowNumber, "date");
        }

        if (!TryParseAmount(raw.Amount, numberFormat, out var amount))
        {
            throw InvalidRow(rowNumber, "amount");
        }

        var description = Whitespace().Replace(raw.Description, " ").Trim();
        if (description.Length == 0)
        {
            throw InvalidRow(rowNumber, "description");
        }

        return new Transaction(
            date,
            ApplyDirection(amount, raw.Direction, signConvention),
            SensitiveDataMasker.Mask(description));
    }

    private static decimal ApplyDirection(decimal amount, RawAmountDirection direction, SignConvention convention) =>
        direction switch
        {
            RawAmountDirection.Debit => -Math.Abs(amount),
            RawAmountDirection.Credit => Math.Abs(amount),
            _ when convention == SignConvention.PositiveIsOutgoing => -amount,
            _ => amount,
        };

    private static bool TryParseAmount(string text, NumberFormatInfo numberFormat, out decimal amount)
    {
        var value = CurrencyMarkers.Aggregate(text, (current, marker) => current.Replace(marker, "", StringComparison.OrdinalIgnoreCase));

        // Drop whitespace and the bidi marks that right-to-left (e.g. Hebrew) pages put around numbers.
        value = new string(value.Where(c => !char.IsWhiteSpace(c) && c is not '\u200E' and not '\u200F').ToArray())
            .Replace('\u2212', '-');

        var negative = false;
        if (value.Length > 2 && value[0] == '(' && value[^1] == ')')
        {
            negative = true;
            value = value[1..^1];
        }
        else if (value.StartsWith('-'))
        {
            negative = true;
            value = value[1..];
        }
        else if (value.EndsWith('-'))
        {
            // Trailing minus, common in Israeli bank statements: "1,250.00-".
            negative = true;
            value = value[..^1];
        }
        else if (value.StartsWith('+'))
        {
            value = value[1..];
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, numberFormat, out amount))
        {
            return false;
        }

        if (negative)
        {
            amount = -amount;
        }

        return true;
    }

    private static ExtractionException InvalidRow(int rowNumber, string field) =>
        new($"Transaction row {rowNumber} has an invalid or missing {field}. No transactions were exported.");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

using System.Text.RegularExpressions;

namespace Sumit.TransactionExporter.Application;

public static partial class SensitiveDataMasker
{
    // Eight or more digits, optionally grouped by single spaces or dashes: card, account, IBAN and
    // phone numbers. Shorter numbers (amounts, branch codes, references) stay readable. A digit-only
    // date such as 2026-10-05 also matches; over-masking is the safer mistake. \d and char.IsDigit
    // both accept any Unicode decimal digit (e.g. Arabic-Indic), so those numbers are masked as well.
    [GeneratedRegex(@"\d(?:[ -]?\d){7,}")]
    private static partial Regex LongNumber();

    public static string Mask(string text) =>
        LongNumber().Replace(text, match =>
        {
            var digits = match.Value.Where(char.IsDigit).ToArray();
            return "****" + new string(digits[^4..]);
        });
}

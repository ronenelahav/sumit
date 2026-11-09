namespace Sumit.TransactionExporter.Domain;

/// <summary>A half-open date range: <see cref="Start"/> is included, <see cref="End"/> is excluded.</summary>
public sealed record DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentException("The end date must be after the start date.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    /// <summary>The last date inside the range, for UIs and websites that use inclusive end dates.</summary>
    public DateOnly LastDay => End.AddDays(-1);

    public bool Contains(DateOnly date) => date >= Start && date < End;

    public static DateRange PreviousCalendarMonth(DateOnly today)
    {
        var startOfThisMonth = new DateOnly(today.Year, today.Month, 1);
        return new DateRange(startOfThisMonth.AddMonths(-1), startOfThisMonth);
    }
}

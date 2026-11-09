using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Tests.Domain;

public class DateRangeTests
{
    [Fact]
    public void PreviousCalendarMonth_InJanuary_ReturnsDecemberOfPreviousYear()
    {
        var range = DateRange.PreviousCalendarMonth(new DateOnly(2027, 1, 15));

        Assert.Equal(new DateOnly(2026, 12, 1), range.Start);
        Assert.Equal(new DateOnly(2027, 1, 1), range.End);
        Assert.Equal(new DateOnly(2026, 12, 31), range.LastDay);
    }
}

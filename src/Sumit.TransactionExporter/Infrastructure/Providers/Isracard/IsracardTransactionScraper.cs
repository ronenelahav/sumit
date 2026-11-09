using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;

namespace Sumit.TransactionExporter.Infrastructure.Providers.Isracard;

/// <summary>
/// Isracard lists transactions per billing month, while the export covers purchases in a calendar
/// month. Purchases are billed in the same month or up to two months later (deferred charges and
/// installments), so those billing months are read and the normalizer keeps rows dated in the period:
/// the purchase date, or for later installments the month they are allocated to (see
/// <see cref="IsracardTransactionMapper"/>).
/// </summary>
public sealed class IsracardTransactionScraper(
    IOptionsMonitor<ProviderSettings> providerSettings,
    TimeProvider timeProvider,
    ILogger<IsracardTransactionScraper> logger) : ITransactionScraper
{
    private const int BillingMonthsAfterPeriod = 2;

    public FinancialProvider Provider => FinancialProvider.Isracard;

    public RawTransactionFormat Format { get; } = new(["dd/MM/yyyy"], ".", ",", SignConvention.PositiveIsOutgoing);

    public async Task<IReadOnlyList<RawTransaction>> ScrapeAsync(
        IBrowserSession session,
        DateRange period,
        CancellationToken cancellationToken)
    {
        var api = new IsracardApi(providerSettings.Get(Provider.ToString()).LoginUrl);
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var transactions = new List<RawTransaction>();

        var dataTab = await IsracardApi.OpenDataTabAsync(session.Page);
        try
        {
            foreach (var billingMonth in BillingMonthsToRead(period, today))
            {
                cancellationToken.ThrowIfCancellationRequested();
                transactions.AddRange(await ReadBillingMonthAsync(api, dataTab, billingMonth));
            }
        }
        finally
        {
            await dataTab.CloseAsync();
        }

        return transactions;
    }

    private async Task<IReadOnlyList<RawTransaction>> ReadBillingMonthAsync(IsracardApi api, IPage dataTab, DateOnly billingMonth)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"reqName=CardsTransactionsList&month={billingMonth:MM}&year={billingMonth:yyyy}&requiredDate=N");
        using var response = await api.GetAsync(dataTab, query)
            ?? throw new ExtractionException(
                "Isracard did not return transaction data (the response was not JSON). Please try again.");
        if (!IsracardApi.IsSuccess(response.RootElement))
        {
            throw new ExtractionException(
                $"Isracard refused the transaction request (status {IsracardApi.Status(response.RootElement)}). The session may have expired; please sign in again.");
        }

        var rows = IsracardTransactionMapper.Map(response.RootElement);
        logger.LogDebug("Isracard billing month {BillingMonth:yyyy-MM}: {RowCount} rows", billingMonth, rows.Count);
        return rows;
    }

    /// <summary>
    /// First day of each billing month to read: the period's months plus the following
    /// <see cref="BillingMonthsAfterPeriod"/>, but never past next month's upcoming charges.
    /// </summary>
    public static IReadOnlyList<DateOnly> BillingMonthsToRead(DateRange period, DateOnly today)
    {
        var first = new DateOnly(period.Start.Year, period.Start.Month, 1);
        var lastInPeriod = new DateOnly(period.LastDay.Year, period.LastDay.Month, 1);
        var nextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);

        var last = lastInPeriod.AddMonths(BillingMonthsAfterPeriod);
        if (last > nextMonth)
        {
            last = nextMonth;
        }

        var months = new List<DateOnly>();
        for (var month = first; month <= last; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        return months;
    }
}

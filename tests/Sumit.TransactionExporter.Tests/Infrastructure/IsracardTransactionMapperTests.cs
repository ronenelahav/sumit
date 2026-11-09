using System.Text.Json;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Infrastructure.Providers.Isracard;

namespace Sumit.TransactionExporter.Tests.Infrastructure;

/// <summary>Synthetic responses shaped like Isracard's CardsTransactionsList; all values are invented.</summary>
public class IsracardTransactionMapperTests
{
    [Fact]
    public void Map_ReadsDomesticAndAbroadRowsOfEveryCard()
    {
        var response = Parse("""
            {
              "Header": { "Status": "1" },
              "CardsTransactionsListBean": {
                "Index0": { "CurrentCardTransactions": [ {
                  "txnIsrael": [ { "fullPurchaseDate": "05/10/2026", "paymentSum": 125.5, "fullSupplierNameHeb": "סופר דמו",
                                   "dealSumOutbound": null, "voucherNumberRatz": "111111111", "voucherNumberRatzOutbound": null } ],
                  "txnAbroad": [ { "fullPurchaseDateOutbound": "07/10/2026", "paymentSumOutbound": 40, "fullSupplierNameOutbound": "DEMO STORE",
                                   "dealSumOutbound": 10.99, "voucherNumberRatz": null, "voucherNumberRatzOutbound": "222222222" } ]
                } ] },
                "Index1": { "CurrentCardTransactions": [ {
                  "txnIsrael": [ { "fullPurchaseDate": "09/10/2026", "paymentSum": "18.00", "fullSupplierNameHeb": "קפה דמו",
                                   "voucherNumberRatz": "333333333" } ]
                } ] },
                "cardsSummary": { "CurrentCardTransactions": [] }
              }
            }
            """);

        Assert.Equal(
            [
                new RawTransaction("05/10/2026", "125.5", "סופר דמו"),
                new RawTransaction("07/10/2026", "40", "DEMO STORE"),
                new RawTransaction("09/10/2026", "18.00", "קפה דמו"),
            ],
            IsracardTransactionMapper.Map(response));
    }

    [Fact]
    public void Map_MissingTransactionStructure_Throws()
    {
        var response = Parse("""{ "Header": { "Status": "1" } }""");

        var exception = Assert.Throws<ExtractionException>(() => IsracardTransactionMapper.Map(response));

        Assert.Contains("unexpected format", exception.Message);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
}

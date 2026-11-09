using System.Text.Json;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Infrastructure.Providers.Mizrahi;

namespace Sumit.TransactionExporter.Tests.Infrastructure;

/// <summary>Synthetic responses shaped like Mizrahi Tefahot's get428Index; all values are invented.</summary>
public class MizrahiTransactionMapperTests
{
    [Fact]
    public void Map_ReadsTransactionRowsAndSkipsTheBalanceLine()
    {
        var response = JsonDocument.Parse("""
            { "header": { "success": null, "messages": [] },
              "body": { "table": { "rows": [
                { "RecTypeSpecified": false, "MC02PeulaTaaEZ": null, "MC02SchumEZ": null, "MC02TnuaTeurEZ": "יתרה קודמת" },
                { "RecTypeSpecified": true, "MC02PeulaTaaEZ": "2026-10-03T00:00:00", "MC02SchumEZ": -250.5, "MC02TnuaTeurEZ": "כרטיס אשראי" },
                { "RecTypeSpecified": true, "MC02PeulaTaaEZ": "2026-10-10T00:00:00", "MC02SchumEZ": 12000, "MC02TnuaTeurEZ": "משכורת" }
              ], "isHasMoreRows": false }, "fields": null } }
            """).RootElement;

        Assert.Equal(
            [
                new RawTransaction("2026-10-03", "-250.5", "כרטיס אשראי"),
                new RawTransaction("2026-10-10", "12000", "משכורת"),
            ],
            MizrahiTransactionMapper.Map(response));
    }
}

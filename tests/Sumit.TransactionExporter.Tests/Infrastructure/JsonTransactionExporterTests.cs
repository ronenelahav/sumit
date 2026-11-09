using System.Text;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Export;

namespace Sumit.TransactionExporter.Tests.Infrastructure;

public class JsonTransactionExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesTheDocumentedSchema()
    {
        Transaction[] transactions =
        [
            new(new DateOnly(2026, 10, 5), -125.50m, "Grocery purchase"),
            new(new DateOnly(2026, 10, 10), 3500.00m, "Incoming transfer"),
        ];
        using var stream = new MemoryStream();

        await new JsonTransactionExporter().ExportAsync(transactions, stream, CancellationToken.None);

        var expected = """
            [
              {
                "date": "2026-10-05",
                "amount": -125.50,
                "description": "Grocery purchase"
              },
              {
                "date": "2026-10-10",
                "amount": 3500.00,
                "description": "Incoming transfer"
              }
            ]
            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n"));
    }
}

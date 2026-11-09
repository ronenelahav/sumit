using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Sumit.TransactionExporter.Application.Abstractions;
using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Infrastructure.Export;

public sealed class JsonTransactionExporter : ITransactionExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Keep non-Latin descriptions (e.g. Hebrew) readable instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public Task ExportAsync(
        IReadOnlyList<Transaction> transactions,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var documents = transactions.Select(t => new TransactionDocument(t.Date, t.Amount, t.Description)).ToList();
        return JsonSerializer.SerializeAsync(destination, documents, Options, cancellationToken);
    }

    // The file schema is owned here, so renaming a domain property cannot silently change it.
    private sealed record TransactionDocument(
        [property: JsonPropertyName("date")] DateOnly Date,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("description")] string Description);
}

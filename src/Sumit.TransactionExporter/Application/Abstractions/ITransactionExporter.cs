using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Application.Abstractions;

public interface ITransactionExporter
{
    Task ExportAsync(
        IReadOnlyList<Transaction> transactions,
        Stream destination,
        CancellationToken cancellationToken);
}

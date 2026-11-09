using Sumit.TransactionExporter.Domain;

namespace Sumit.TransactionExporter.Application;

public sealed record ProviderInfo(FinancialProvider Provider, string DisplayName, ProviderKind Kind);

public enum ProviderKind
{
    BankAccount,
    CreditCard,
}

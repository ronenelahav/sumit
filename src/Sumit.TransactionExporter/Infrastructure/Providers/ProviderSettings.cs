using System.ComponentModel.DataAnnotations;

namespace Sumit.TransactionExporter.Infrastructure.Providers;

/// <summary>Per-provider configuration, bound from <c>Providers:{FinancialProvider}</c>.</summary>
public sealed class ProviderSettings
{
    [Required]
    public string DisplayName { get; set; } = "";

    [Required]
    [Url]
    public string LoginUrl { get; set; } = "";
}

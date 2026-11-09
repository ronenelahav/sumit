using Microsoft.Extensions.Options;
using Sumit.TransactionExporter.Application;
using Sumit.TransactionExporter.Application.Abstractions;
using Sumit.TransactionExporter.Domain;
using Sumit.TransactionExporter.Infrastructure.Browser;
using Sumit.TransactionExporter.Infrastructure.Export;
using Sumit.TransactionExporter.Infrastructure.Providers;
using Sumit.TransactionExporter.Infrastructure.Providers.Isracard;
using Sumit.TransactionExporter.Infrastructure.Providers.Mizrahi;

namespace Sumit.TransactionExporter.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BrowserOptions>().Bind(configuration.GetSection("Browser"));
        services.AddTransient<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        services.AddTransient<ProviderRegistry>();
        services.AddTransient<ITransactionSource, BrowserTransactionSource>();
        services.AddTransient<ITransactionExporter, JsonTransactionExporter>();

        services.AddProvider<IsracardAuthenticator, IsracardTransactionScraper>(
            FinancialProvider.Isracard, ProviderKind.CreditCard, configuration);
        services.AddProvider<MizrahiAuthenticator, MizrahiTransactionScraper>(
            FinancialProvider.Mizrahi, ProviderKind.BankAccount, configuration);

        return services;
    }

    private static void AddProvider<TAuthenticator, TScraper>(
        this IServiceCollection services,
        FinancialProvider provider,
        ProviderKind kind,
        IConfiguration configuration)
        where TAuthenticator : class, IProviderAuthenticator
        where TScraper : class, ITransactionScraper
    {
        var name = provider.ToString();
        services.AddOptions<ProviderSettings>(name)
            .Bind(configuration.GetSection($"Providers:{name}"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTransient<IProviderAuthenticator, TAuthenticator>();
        services.AddTransient<ITransactionScraper, TScraper>();
        services.AddTransient(sp => new ProviderInfo(
            provider,
            sp.GetRequiredService<IOptionsMonitor<ProviderSettings>>().Get(name).DisplayName,
            kind));
    }
}

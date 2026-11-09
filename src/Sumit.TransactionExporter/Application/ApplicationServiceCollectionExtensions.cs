namespace Sumit.TransactionExporter.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<TransactionNormalizer>();
        services.AddTransient<TransactionExportService>();
        return services;
    }
}

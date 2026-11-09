using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sumit.TransactionExporter.Infrastructure.Browser;

/// <summary>
/// <c>dotnet run -- --check-browser</c>. Opens a sign-in window and prints why each browser was skipped.
/// </summary>
public static class BrowserLaunchCheck
{
    public static async Task RunAsync(string[] args)
    {
        Console.WriteLine("Checking whether a visible sign-in window can open.");
        var playwrightFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
        Console.WriteLine($"Playwright browser folder: {playwrightFolder}");
        var installed = Directory.Exists(playwrightFolder)
            ? Directory.GetDirectories(playwrightFolder).Select(Path.GetFileName)
            : [];
        Console.WriteLine(installed.Any()
            ? "Installed Playwright browsers: " + string.Join(", ", installed)
            : "Installed Playwright browsers: none. That download is optional when Chrome is installed.");
        if (OperatingSystem.IsWindows())
        {
            var browsers = InstalledBrowser.Find();
            Console.WriteLine(browsers.Count == 0
                ? "Installed Chrome or Edge: not found in the usual folders."
                : "Installed Chrome or Edge: " + string.Join(", ", browsers.Select(browser => $"{browser.Name} at {browser.Path}")));
        }

        var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--check-browser").ToArray());
        // Shows the launch paths and error reasons that the app logs at Debug only.
        builder.Logging.AddFilter(typeof(PlaywrightBrowserSessionFactory).FullName, LogLevel.Debug);
        builder.Services.AddOptions<BrowserOptions>().Bind(builder.Configuration.GetSection("Browser"));
        builder.Services.PostConfigure<BrowserOptions>(options => options.Headless = false);
        builder.Services.AddTransient<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        await using var app = builder.Build();

        Console.WriteLine("Trying " + string.Join(", then ", PlaywrightBrowserSessionFactory.HeadedAttempts().Select(attempt => attempt.Name)) + ".");
        try
        {
            var factory = app.Services.GetRequiredService<IBrowserSessionFactory>();
            await using var session = await factory.CreateAsync(CancellationToken.None);
            await session.Page.GotoAsync("about:blank");
            Console.WriteLine("A browser window is open. It stays up for 15 seconds, then this check closes it.");
            await Task.Delay(TimeSpan.FromSeconds(15));
            Console.WriteLine("Check finished. Connect in the app uses the same browser.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex is Sumit.TransactionExporter.Application.ExtractionException ? ex.Message : PlaywrightBrowserSessionFactory.FirstLine(ex));
            Environment.ExitCode = 1;
        }
    }
}

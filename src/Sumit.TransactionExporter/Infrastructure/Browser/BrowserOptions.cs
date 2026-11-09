namespace Sumit.TransactionExporter.Infrastructure.Browser;

public sealed class BrowserOptions
{
    /// <summary>Real sign-in needs a visible window; headless is only useful for automated tests.</summary>
    public bool Headless { get; set; }

    /// <summary>How long the user has to finish signing in, including MFA.</summary>
    public TimeSpan AuthenticationTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Default timeout for individual page actions and waits while scraping.</summary>
    public TimeSpan ActionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

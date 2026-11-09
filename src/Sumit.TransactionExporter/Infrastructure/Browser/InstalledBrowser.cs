using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Sumit.TransactionExporter.Infrastructure.Browser;

/// <summary>
/// Finds Google Chrome or Microsoft Edge already installed on this computer.
/// No separate Playwright browser download is required when one of these exists.
/// </summary>
internal static class InstalledBrowser
{
    public readonly record struct Location(string Name, string Path);

    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<Location> Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var found = new List<Location>();
        var chrome = FirstExisting(ChromeCandidates());
        if (chrome is not null)
        {
            found.Add(new("chrome", chrome));
        }

        var edge = FirstExisting(EdgeCandidates());
        if (edge is not null)
        {
            found.Add(new("edge", edge));
        }

        return found;
    }

    internal static string? FirstExisting(IEnumerable<string> candidates) =>
        candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> ChromeCandidates()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
        var registered = RegisteredExecutable("chrome.exe");
        if (registered is not null)
        {
            yield return registered;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> EdgeCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe");
        var registered = RegisteredExecutable("msedge.exe");
        if (registered is not null)
        {
            yield return registered;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? RegisteredExecutable(string fileName)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{fileName}");
            if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path))
            {
                return path.Trim().Trim('"');
            }
        }

        return null;
    }
}

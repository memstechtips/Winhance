using System.Globalization;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

internal static class WinGetProgressParser
{
    public enum WinGetPhase
    {
        Found,
        Downloading,
        Installing,
        Uninstalling,
        Complete,
        Error
    }

    public record WinGetProgressInfo(WinGetPhase Phase, double? Percent);

    // Matches patterns like "██████████████████████████████  100%" or " 52.3 MB /  80.1 MB" or percentage
    private static readonly Regex PercentRegex = new(@"(\d+\.?\d*)\s*%", RegexOptions.Compiled);

    // Matches byte progress like "1.2 MB / 5.4 MB"
    private static readonly Regex ByteProgressRegex = new(
        @"([\d.]+)\s*[KMGT]?B\s*/\s*([\d.]+)\s*[KMGT]?B",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches "(n/m) " prefix used by bundled winget for dependency progress
    private static readonly Regex StepPrefixRegex = new(@"^\((\d+)/(\d+)\)\s+(.+)$", RegexOptions.Compiled);

    // Matches "ReportIdentityFound {Name} [{Id}] ShowVersion {Version}"
    private static readonly Regex ReportIdentityRegex = new(
        @"ReportIdentityFound\s+(.+?)\s+\[.+?\]\s+ShowVersion\s+(.+)",
        RegexOptions.Compiled);

    // Matches "Downloading https://..." (extract filename from URL)
    private static readonly Regex DownloadUrlRegex = new(
        @"^Downloading\s+(https?://.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches hex error codes like "0x8a15000f : ..."
    private static readonly Regex HexErrorRegex = new(
        @"^0x[0-9a-fA-F]+\s*:\s*(.+)",
        RegexOptions.Compiled);

    // Empty string = suppress the line entirely.
    private static readonly Dictionary<string, string> ResourceKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["InstallationDisclaimer1"] = "",
        ["InstallationDisclaimer2"] = "",
        ["PackageRequiresDependencies"] = "Installing dependencies...",
        ["PackageDependencies"] = "",
        ["InstallerHashVerified"] = "Hash verified",
        ["ExtractingArchive"] = "Extracting archive...",
        ["ExtractArchiveSucceeded"] = "Archive extracted",
        ["InstallFlowStartingPackageInstall"] = "Installing...",
        ["InstallFlowInstallSuccess"] = "Installation successful",
        ["SourceOpenFailedSuggestion"] = "WinGet source unavailable",
        ["UnexpectedErrorExecutingCommand"] = "Unexpected error",
        ["InstallingDependencies"] = "Installing dependencies...",
    };

    // A translation that throws must not take the install down with it.
    public static void Forward(string line, ITaskProgressService? progress, ILogService? log)
    {
        try
        {
            var displayLine = TranslateLine(line);
            progress?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = displayLine ?? line,
                IsProgressIndicator = true
            });
        }
        catch (Exception ex)
        {
            log?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
        }
    }

    public static string? TranslateLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var trimmed = line.Trim();

        var stepMatch = StepPrefixRegex.Match(trimmed);
        if (stepMatch.Success)
        {
            var n = stepMatch.Groups[1].Value;
            var m = stepMatch.Groups[2].Value;
            var rest = stepMatch.Groups[3].Value;
            var translatedRest = TranslateCore(rest);
            if (translatedRest == null)
                return null;
            return string.IsNullOrEmpty(translatedRest) ? null : $"[{n}/{m}] {translatedRest}";
        }

        return TranslateCore(trimmed);
    }

    private static string? TranslateCore(string trimmed)
    {
        var identityMatch = ReportIdentityRegex.Match(trimmed);
        if (identityMatch.Success)
        {
            var name = identityMatch.Groups[1].Value.Trim();
            var version = identityMatch.Groups[2].Value.Trim();
            return $"Found: {name} v{version}";
        }

        var downloadMatch = DownloadUrlRegex.Match(trimmed);
        if (downloadMatch.Success)
        {
            try
            {
                var uri = new System.Uri(downloadMatch.Groups[1].Value.Trim());
                var filename = System.IO.Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(filename))
                    return $"Downloading {filename}...";
            }
            catch { }
            return "Downloading...";
        }

        if (ResourceKeyMap.TryGetValue(trimmed, out var mapped))
        {
            return string.IsNullOrEmpty(mapped) ? null : mapped;
        }

        // Dependency detail lines (indented with "  - " or deep indent) — pass through
        if (trimmed.StartsWith("  - ") || trimmed.StartsWith("      "))
            return trimmed;

        var hexMatch = HexErrorRegex.Match(trimmed);
        if (hexMatch.Success)
            return $"Error: {hexMatch.Groups[1].Value.Trim()}";

        return trimmed;
    }

    public static WinGetProgressInfo? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var trimmed = line.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower.Contains("found ") || lower.Contains("package found"))
            return new WinGetProgressInfo(WinGetPhase.Found, null);

        if (lower.Contains("successfully installed") || lower.Contains("installation successful"))
            return new WinGetProgressInfo(WinGetPhase.Complete, 100);

        if (lower.Contains("successfully uninstalled") || lower.Contains("uninstall successful"))
            return new WinGetProgressInfo(WinGetPhase.Complete, 100);

        if (lower.Contains("no applicable") || lower.Contains("no package found") ||
            lower.Contains("no installed package"))
            return new WinGetProgressInfo(WinGetPhase.Error, null);

        if (lower.Contains("uninstalling"))
            return new WinGetProgressInfo(WinGetPhase.Uninstalling, null);

        var percentMatch = PercentRegex.Match(trimmed);
        if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
        {
            var phase = lower.Contains("download") || lower.Contains("██")
                ? WinGetPhase.Downloading
                : lower.Contains("install")
                    ? WinGetPhase.Installing
                    : WinGetPhase.Downloading; // default to downloading for progress bars

            return new WinGetProgressInfo(phase, pct);
        }

        var byteMatch = ByteProgressRegex.Match(trimmed);
        if (byteMatch.Success &&
            double.TryParse(byteMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current) &&
            double.TryParse(byteMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var total) &&
            total > 0)
        {
            var bytePct = (current / total) * 100.0;
            return new WinGetProgressInfo(WinGetPhase.Downloading, bytePct);
        }

        if (lower.Contains("downloading"))
            return new WinGetProgressInfo(WinGetPhase.Downloading, null);

        if (lower.Contains("installing") || lower.Contains("starting package install"))
            return new WinGetProgressInfo(WinGetPhase.Installing, null);

        // Resource key phase detection (bundled winget outputs keys instead of localized text)
        if (lower.Contains("installflowstartingpackageinstall"))
            return new WinGetProgressInfo(WinGetPhase.Installing, null);
        if (lower.Contains("installflowinstallsuccess"))
            return new WinGetProgressInfo(WinGetPhase.Complete, 100);
        if (lower == "extractingarchive" || lower == "extractarchivesucceeded")
            return new WinGetProgressInfo(WinGetPhase.Installing, null);
        if (lower.Contains("reportidentityfound"))
            return new WinGetProgressInfo(WinGetPhase.Found, null);
        if (lower.Contains("installerhashverified"))
            return new WinGetProgressInfo(WinGetPhase.Installing, null);

        return null;
    }
}

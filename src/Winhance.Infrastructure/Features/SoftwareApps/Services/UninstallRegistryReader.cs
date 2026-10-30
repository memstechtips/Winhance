using System.Globalization;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// One key under an Uninstall root. DisplayName and UninstallString are empty when the key has none.
internal sealed record UninstallEntry(string KeyName, bool PerUser, string DisplayName, string UninstallString)
{
    public bool IsSystemComponent { get; init; }

    public bool IsUpdate { get; init; }

    public bool IsListed =>
        !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(UninstallString) && !IsSystemComponent && !IsUpdate;

    public string? Publisher { get; init; }

    public string? Version { get; init; }

    public DateTime? InstalledOn { get; init; }

    public long? SizeBytes { get; init; }
}

internal static class UninstallRegistryReader
{
    internal const string Machine = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    internal const string Machine32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
    internal const string User = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly HashSet<string> UpdateReleaseTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Update", "Hotfix", "Security Update", "Service Pack",
    };

    private static readonly (string Root, bool PerUser)[] Roots = [(Machine, false), (Machine32, false), (User, true)];

    // The rows Programs and Features lists: a name and an uninstall command, not a system component, not an update
    // hanging off another program. EstimatedSize is in KB; InstallDate is yyyyMMdd text.
    internal static IReadOnlyList<UninstallEntry> Read(IWindowsRegistryService registry) =>
        Keys(registry)
            .Where(key => key.Entry.IsListed)
            .Select(key => key.Entry with
            {
                DisplayName = key.Entry.DisplayName.Trim(),
                Publisher = Text(registry.GetValue(key.Path, "Publisher")),
                Version = Text(registry.GetValue(key.Path, "DisplayVersion")),
                InstalledOn = DateTime.TryParseExact(Text(registry.GetValue(key.Path, "InstallDate")), "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
                SizeBytes = registry.GetValue(key.Path, "EstimatedSize") is int kb and > 0 ? kb * 1024L : null,
            })
            .ToList();

    // Every key, hidden ones included, machine keys before the user's: detection matches on key names Programs and
    // Features never shows, and an uninstall takes the first matching key that has a command.
    internal static IEnumerable<UninstallEntry> ReadAll(IWindowsRegistryService registry) => Keys(registry).Select(key => key.Entry);

    private static IEnumerable<(string Path, UninstallEntry Entry)> Keys(IWindowsRegistryService registry)
    {
        foreach (var (root, perUser) in Roots)
        {
            foreach (var keyName in registry.GetSubKeyNames(root))
            {
                var path = $@"{root}\{keyName}";
                yield return (path, new UninstallEntry(
                    keyName,
                    perUser,
                    registry.GetValue(path, "DisplayName") as string ?? string.Empty,
                    registry.GetValue(path, "UninstallString") as string ?? string.Empty)
                {
                    IsSystemComponent = registry.GetValue(path, "SystemComponent") is 1,
                    IsUpdate = registry.GetValue(path, "ParentKeyName") is string { Length: > 0 }
                        || (registry.GetValue(path, "ReleaseType") is string releaseType && UpdateReleaseTypes.Contains(releaseType)),
                });
            }
        }
    }

    private static string? Text(object? value) => value is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
}

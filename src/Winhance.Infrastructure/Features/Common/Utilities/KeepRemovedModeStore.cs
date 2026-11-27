using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.Common.Utilities;

// The kept list and its two settings share one registry key; every other value name is a catalog app id.
internal static class KeepRemovedModeStore
{
    internal const string Key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\KeepRemoved";
    internal const string ModeValue = "Mode";
    internal const string EnabledValue = "Enabled";

    // Scheduled task name == script name; Edge and WebView2 share one script.
    internal static readonly string[] RemovalTaskNames =
    [
        BloatRemovalScriptGenerator.ScriptName,
        .. WindowsAppDefinitions.GetWindowsApps().Items.Select(a => a.RemovalScriptName).OfType<string>().Distinct(),
    ];

    internal static bool IsSetting(string valueName) =>
        valueName.Equals(ModeValue, StringComparison.OrdinalIgnoreCase)
        || valueName.Equals(EnabledValue, StringComparison.OrdinalIgnoreCase);

    internal static string ScriptPath(IFileSystemService fileSystem, string taskName) =>
        fileSystem.CombinePath(ScriptPaths.ScriptsDirectory, taskName + ".ps1");

    // No value yet: a PC that already has removal scripts keeps working as it does; any other PC starts in Winhance mode.
    internal static KeepRemovedMode Read(IWindowsRegistryService registry, IFileSystemService fileSystem) =>
        registry.GetValue(Key, ModeValue) as string is { } text && Enum.TryParse<KeepRemovedMode>(text, out var mode) && Enum.IsDefined(mode)
            ? mode
            : RemovalTaskNames.Any(name => fileSystem.FileExists(ScriptPath(fileSystem, name)))
                ? KeepRemovedMode.Scripts
                : KeepRemovedMode.Winhance;
}

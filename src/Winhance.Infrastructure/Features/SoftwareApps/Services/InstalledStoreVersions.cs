using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// Get-AppxPackage reports the main package inside a bundle, whose version often differs from the bundle's own, and the
// catalog lists the bundle. The registry names the bundle: Name_Version_Arch_~_PublisherId, or Name_Version_Arch__PublisherId
// for a single package.
internal static class InstalledStoreVersions
{
    internal const string Applications = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications";

    // Per package family name, the highest version registered.
    internal static Dictionary<string, ulong> Read(IWindowsRegistryService registry)
    {
        var versions = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in registry.GetSubKeyNames(Applications))
        {
            if (TryParseKeyName(name, out var family, out var version)
                && (!versions.TryGetValue(family, out var known) || version > known))
                versions[family] = version;
        }

        return versions;
    }

    // Resource and architecture-split packages carry a resource id of their own and are not the bundle.
    internal static bool TryParseKeyName(string keyName, out string familyName, out ulong version)
    {
        familyName = string.Empty;
        version = 0;
        var parts = keyName.Split('_');
        if (parts.Length != 5 || parts[4].Length == 0 || parts[3] is not ("" or "~")
            || !PackedVersion.TryFromDotted(parts[1], out version))
            return false;

        familyName = $"{parts[0]}_{parts[4]}";
        return true;
    }
}

using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDetectionContext
{
    WinBuild CurrentBuild { get; }

    object? GetValue(string keyPath, string? valueName);

    // Only used to describe a malformed value in the log; detection never branches on it.
    Microsoft.Win32.RegistryValueKind? GetValueKind(string keyPath, string? valueName) => null;

    string[] GetSubKeyNames(string keyPath);

    bool KeyExists(string keyPath);

    string? PrimaryDnsV4OfActiveAdapter();

    // In adapter order, so a caller can read the secondary as well as the primary. Same DHCP rule as
    // PrimaryDnsV4OfActiveAdapter: an adapter that leased its servers reports none.
    IReadOnlyList<string> DnsV4ServersOfActiveAdapter();

    bool IsSystemRestoreEnabled();

    // Null when the task does not exist on this system.
    bool? ScheduledTaskEnabled(string taskPath);

    int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context);

    string? ActivePowerPlanGuid();

    IReadOnlyList<DynamicOption> InstalledPowerPlans() => System.Array.Empty<DynamicOption>();

    // The Disabled update-policy state renames Windows Update's critical DLLs to _BAK - a filesystem check the registry cannot express.
    bool CriticalUpdateDllsRenamed() => false;

    // The slideshow album is stored as a shell item list, which only the shell can turn back into a folder path.
    string? ShellFolderPath(byte[] itemList) => null;

    // The recent pictures are what Windows recorded, so a file since deleted must not become a tile.
    bool FileExists(string path) => true;
}

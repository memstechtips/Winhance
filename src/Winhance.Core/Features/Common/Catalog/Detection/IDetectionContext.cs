namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The platform reads a custom detector needs, abstracted so detectors are unit-testable without
/// touching the real registry or network. The real implementation wraps the Windows registry + network
/// services.</summary>
public interface IDetectionContext
{
    /// <summary>The running Windows build. Used to select build-applicable targets (Target.AppliesTo).</summary>
    WinBuild CurrentBuild { get; }

    /// <summary>Read a registry value, or null if the key/value is absent.</summary>
    object? GetValue(string keyPath, string? valueName);

    /// <summary>The stored registry TYPE of a value (REG_BINARY, REG_SZ, ...), or null when the value is
    /// absent or the type cannot be read. Used only to describe a malformed value in the log - detection
    /// itself never branches on it. Default null: only the live context reads it; test fakes report
    /// "unknown" and the diagnostic falls back to naming the CLR type.</summary>
    Microsoft.Win32.RegistryValueKind? GetValueKind(string keyPath, string? valueName) => null;

    /// <summary>The immediate sub-key names under a registry key (empty if the key is absent).</summary>
    string[] GetSubKeyNames(string keyPath);

    /// <summary>Whether a registry key exists. A setting whose ValueName is null encodes its state as
    /// key presence rather than a stored value.</summary>
    bool KeyExists(string keyPath);

    /// <summary>The active network adapter's primary IPv4 DNS server, or null when DNS is automatic (DHCP)
    /// or there is no active adapter.</summary>
    string? PrimaryDnsV4OfActiveAdapter();

    /// <summary>Whether System Restore is enabled for the system drive.</summary>
    bool IsSystemRestoreEnabled();

    /// <summary>The enabled state of a scheduled task: true (enabled), false (disabled), or null when the task
    /// does not exist on this system.</summary>
    bool? ScheduledTaskEnabled(string taskPath);

    /// <summary>The current AC or DC value index of a powercfg setting on the active power scheme, or null when
    /// the setting is not present / not readable. A powercfg target reads its value through this, once per context.</summary>
    int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context);

    /// <summary>The active power scheme's GUID as a string (lowercase), or null when there is no active scheme. Used
    /// by the power-plan detector; unrelated to the per-setting powercfg value reads.</summary>
    string? ActivePowerPlanGuid();

    /// <summary>The active power scheme's RAW OS name (e.g. "Balanced", or a custom plan's actual name), read from
    /// GetAvailablePowerPlansAsync's active plan. Null when there is no active scheme. Default null: only the live
    /// context reads it; test fakes have no plan.</summary>
    string? ActivePowerPlanName() => null;

    /// <summary>The machine's installed power plans as dynamic options (Label = plan name, Value = scheme GUID),
    /// pre-fetched per batch like the active plan. Consumed by <see cref="PowerPlanOptionSource"/>. Default empty:
    /// only the live context enumerates them; test fakes have no plans to read.</summary>
    IReadOnlyList<DynamicOption> InstalledPowerPlans() => System.Array.Empty<DynamicOption>();

    /// <summary>Whether Windows Update's critical DLLs have been renamed to their "_BAK" backups (the enforcement of
    /// the Disabled update-policy state - a filesystem check the registry cannot express). Consumed by
    /// <see cref="UpdatePolicyDetector"/>. Default false: only the live context touches the filesystem; test fakes
    /// report "not renamed".</summary>
    bool CriticalUpdateDllsRenamed() => false;
}

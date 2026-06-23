namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The platform reads a custom detector needs, abstracted so detectors are unit-testable without
/// touching the real registry or network. The real implementation wraps the Windows registry + network
/// services. Grows as more detectors are ported.</summary>
public interface IDetectionContext
{
    /// <summary>The running Windows build. Used to select build-applicable targets (Target.AppliesTo).</summary>
    WinBuild CurrentBuild { get; }

    /// <summary>Read a registry value, or null if the key/value is absent.</summary>
    object? GetValue(string keyPath, string? valueName);

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
}

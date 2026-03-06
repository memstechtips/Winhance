
namespace Winhance.Core.Features.Common.Models;

public enum PowerModeSupport
{
    Both,           // Apply same value to AC and DC (current behavior)
    Separate,       // Allow different values for AC and DC
    ACOnly,         // Only applies to AC power
    DCOnly          // Only applies to DC/battery power
}

public record PowerCfgSetting
{
    public string? SubgroupGUIDAlias { get; init; }
    public string SettingGUIDAlias { get; init; } = string.Empty;
    public string SubgroupGuid { get; init; } = string.Empty;
    public string SettingGuid { get; init; } = string.Empty;
    public PowerModeSupport PowerModeSupport { get; init; } = PowerModeSupport.Both;
    public string? Units { get; init; }
    public RegistrySetting? EnablementRegistrySetting { get; init; }
    public int? RecommendedValueAC { get; init; }
    public int? RecommendedValueDC { get; init; }
    public bool CheckForHardwareControl { get; init; } = false;
}

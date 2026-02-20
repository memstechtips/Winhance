
namespace Winhance.Core.Features.Common.Models;

public enum PowerModeSupport
{
    Both,           // Apply same value to AC and DC (current behavior)
    Separate,       // Allow different values for AC and DC
    ACOnly,         // Only applies to AC power
    DCOnly          // Only applies to DC/battery power
}

public class PowerCfgSetting
{
    public string? SubgroupGUIDAlias { get; set; }
    public string SettingGUIDAlias { get; set; } = string.Empty;
    public string SubgroupGuid { get; set; } = string.Empty;
    public string SettingGuid { get; set; } = string.Empty;
    public PowerModeSupport PowerModeSupport { get; set; } = PowerModeSupport.Both;
    public string? Units { get; set; }
    public RegistrySetting? EnablementRegistrySetting { get; set; }
    public int? RecommendedValueAC { get; set; }
    public int? RecommendedValueDC { get; set; }
    public bool CheckForHardwareControl { get; set; } = false;
}
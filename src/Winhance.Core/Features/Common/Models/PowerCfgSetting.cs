
namespace Winhance.Core.Features.Common.Models;

public class PowerCfgSetting
{
    public string? SubgroupGUIDAlias { get; set; }
    public string SettingGUIDAlias { get; set; }
    public string SubgroupGuid { get; set; }
    public string SettingGuid { get; set; }
    public bool ApplyToACDC { get; set; } = true; // AC and DC
    public string? Units { get; set; } // "Minutes", "Percentage", etc.
    public string? TargetPowerPlanGuid { get; set; }
    public bool RequiresPowerPlanCreation { get; set; } = false;
    public string? SourcePowerPlanForCreation { get; set; }
    public RegistrySetting? EnablementRegistrySetting { get; set; }
}
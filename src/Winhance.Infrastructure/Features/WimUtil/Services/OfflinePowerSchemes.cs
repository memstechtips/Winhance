using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Optimize.Services;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Where powercfg keeps a scheme's values in the SYSTEM hive: User\PowerSchemes\<scheme>\<subgroup>\<setting> holds
// ACSettingIndex and DCSettingIndex, and ActivePowerScheme names the active scheme.
internal static class OfflinePowerSchemes
{
    public const string SchemesKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";

    public const string ActiveSchemeValue = "ActivePowerScheme";

    public static string SettingKey(PowerCfgTarget target, Guid scheme) =>
        $@"{SchemesKey}\{scheme:D}\{Guid.Parse(target.SubgroupGuid):D}\{Guid.Parse(target.SettingGuid):D}";

    public static string[] ValueNames(PowerContext context) => context switch
    {
        PowerContext.AC => ["ACSettingIndex"],
        PowerContext.DC => ["DCSettingIndex"],
        _ => ["ACSettingIndex", "DCSettingIndex"],
    };

    // The three schemes Windows ships. Any other plan exists only after setup duplicates Balanced.
    public static bool IsStock(Guid scheme) =>
        PowerPlanActivationService.WindowsDefaultSchemeGuids.Contains(scheme.ToString("D"));

    public static Guid ActiveSchemeOf(IWindowsRegistryService registry) =>
        registry.GetValue(SchemesKey, ActiveSchemeValue) is string active && Guid.TryParse(active, out var scheme)
            ? scheme
            : Guid.Parse(PowerPlanCatalog.BalancedGuid);
}

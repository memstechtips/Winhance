using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Helpers;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// The single projection "what belongs in a per-build Default config, and with what value" - shared by
/// <see cref="DefaultConfigGeneratorTests"/> (which WRITES the two shipped Default configs from it) and
/// <see cref="DefaultConfigConformanceTests"/> (which asserts the shipped files match it), so the generator and
/// the gate cannot disagree. Built on the PRODUCTION default primitives (<see cref="CatalogToggleState.GetDefault"/>,
/// <c>RecommendedSettingsResolver.GetDefaultIndex(setting, build)</c> / <c>BuildPowerCfgApplyValue(useRecommended:
/// false)</c>), mirroring the per-<see cref="ControlKind"/> dispatch of RecommendedConfigConformanceTests - never a
/// hand-copied rule table.
///
/// A setting is projected iff it is AVAILABLE on the target build (maximal machine: hardware/existence-gated
/// settings included - import drops non-applicable ones at the CatalogSettingsRegistry gate) AND carries a
/// WindowsDefault for that build. Actions and the dynamic power-plan selection have no default state and are
/// never projected (the old VM-exported files carried Action rows; a Default config restores STATES).
/// </summary>
internal static class DefaultConfigProjection
{
    /// <summary>The two shipped Default configs and the build each is generated for.</summary>
    internal static readonly (string FileName, WinBuild Build)[] Targets =
    {
        ("Winhance_Default_Config_Windows10_22H2.winhance", new WinBuild(19045)),
        ("Winhance_Default_Config_Windows11_25H2.winhance", new WinBuild(26200)),
    };

    /// <summary>Feature ids under the Customize section, in shipped-file order.</summary>
    internal static readonly string[] CustomizeFeatures =
    {
        ExplorerCustomizationsCatalog.FeatureId,
        StartMenuCustomizationsCatalog.FeatureId,
        TaskbarCustomizationsCatalog.FeatureId,
        WindowsThemeCustomizationsCatalog.FeatureId,
    };

    /// <summary>Feature ids under the Optimize section, in shipped-file order.</summary>
    internal static readonly string[] OptimizeFeatures =
    {
        GamingAndPerformanceOptimizationsCatalog.FeatureId,
        NotificationOptimizationsCatalog.FeatureId,
        PowerOptimizationsCatalog.FeatureId,
        PrivacyOptimizationsCatalog.FeatureId,
        SoundOptimizationsCatalog.FeatureId,
        UpdateOptimizationsCatalog.FeatureId,
    };

    /// <summary>The Default-config item for a setting on a build, or null when the setting does not belong in
    /// that build's Default config (unavailable, or no WindowsDefault to restore).</summary>
    internal static ConfigurationItem? Project(Setting setting, WinBuild build)
    {
        if (!setting.Availability.Allows(build))
            return null;

        switch (setting.Control)
        {
            case ControlKind.Toggle:
            {
                bool? def = CatalogToggleState.GetDefault(setting, build);
                if (def is null)
                    return null;
                return new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Display.Name,
                    IsSelected = def.Value,
                    InputType = InputType.Toggle,
                };
            }

            case ControlKind.Selection:
            {
                // Powercfg selection first: per-context WindowsDefault option INDICES (Separate mode ->
                // ACIndex/DCIndex; combined -> a bare index). No unit conversion - indices, not values.
                var power = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: false);
                if (power is IReadOnlyDictionary<string, object?> acdc)
                {
                    return new ConfigurationItem
                    {
                        Id = setting.Id,
                        Name = setting.Display.Name,
                        InputType = InputType.Selection,
                        PowerSettings = new Dictionary<string, object>
                        {
                            ["ACIndex"] = Convert.ToInt32(acdc["ACValue"]),
                            ["DCIndex"] = Convert.ToInt32(acdc["DCValue"]),
                        },
                    };
                }
                if (power is not null)
                {
                    return new ConfigurationItem
                    {
                        Id = setting.Id,
                        Name = setting.Display.Name,
                        InputType = InputType.Selection,
                        SelectedIndex = Convert.ToInt32(power),
                    };
                }

                // Registry selection: the build-aware WindowsDefault state index.
                int? idx = RecommendedSettingsResolver.GetDefaultIndex(setting, build);
                if (idx is null)
                    return null;
                return new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Display.Name,
                    InputType = InputType.Selection,
                    SelectedIndex = idx.Value,
                };
            }

            case ControlKind.Slider:
            {
                // Numeric sliders are powercfg. BuildPowerCfgApplyValue emits DISPLAY units; the config stores
                // SYSTEM units (the same trap RecommendedConfigConformanceTests documents), so convert.
                var power = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: false);
                if (power is null)
                    return null;
                string units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
                int ToSystem(object? display) =>
                    RecommendedSettingsResolver.ConvertDisplayToSystemUnits(Convert.ToInt32(display), units);

                if (power is IReadOnlyDictionary<string, object?> values)
                {
                    return new ConfigurationItem
                    {
                        Id = setting.Id,
                        Name = setting.Display.Name,
                        InputType = InputType.NumericRange,
                        PowerSettings = new Dictionary<string, object>
                        {
                            ["ACValue"] = ToSystem(values["ACValue"]),
                            ["DCValue"] = ToSystem(values["DCValue"]),
                        },
                    };
                }
                return new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Display.Name,
                    InputType = InputType.NumericRange,
                    PowerSettings = new Dictionary<string, object> { ["Value"] = ToSystem(power) },
                };
            }

            default:
                return null; // Action, PowerPlan (and any future control without a restorable default state)
        }
    }

    /// <summary>Repo path of a shipped config, anchored on the compile-time source path (the
    /// RecommendedConfigConformanceTests trick) so it resolves even with redirected build outputs.</summary>
    internal static string ConfigPath(string fileName)
        => Path.Combine(SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Resources", "Configs", fileName);

    private static string SolutionDir() => RepoPaths.SolutionDir();
}

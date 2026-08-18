using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Helpers;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

// Shared by the generator (writes) and the conformance test (asserts) so they cannot disagree; built on the
// PRODUCTION default primitives, never a hand-copied rule table. A setting is projected iff AVAILABLE on the
// target build (maximal machine; import drops non-applicable ones) AND carries a WindowsDefault for that build.
// Actions and the dynamic power-plan selection are never projected.
internal static class DefaultConfigProjection
{
    internal static readonly (string FileName, WinBuild Build)[] Targets =
    {
        ("Winhance_Default_Config_Windows10_22H2.winhance", new WinBuild(19045)),
        ("Winhance_Default_Config_Windows11_25H2.winhance", new WinBuild(26200)),
    };

    internal static readonly string[] CustomizeFeatures =
    {
        ExplorerCustomizationsCatalog.FeatureId,
        StartMenuCustomizationsCatalog.FeatureId,
        TaskbarCustomizationsCatalog.FeatureId,
        WindowsThemeCustomizationsCatalog.FeatureId,
    };

    internal static readonly string[] OptimizeFeatures =
    {
        GamingAndPerformanceOptimizationsCatalog.FeatureId,
        NotificationOptimizationsCatalog.FeatureId,
        PowerOptimizationsCatalog.FeatureId,
        PrivacyOptimizationsCatalog.FeatureId,
        SoundOptimizationsCatalog.FeatureId,
        UpdateOptimizationsCatalog.FeatureId,
    };

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

    // Anchored on the compile-time source path so it resolves even with redirected build outputs.
    internal static string ConfigPath(string fileName)
        => Path.Combine(SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Resources", "Configs", fileName);

    private static string SolutionDir() => RepoPaths.SolutionDir();
}

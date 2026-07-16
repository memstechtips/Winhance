using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Optimize.Models;

/// <summary>Shared powercfg ComboBox option-sets (label loc-key + PowerCfgValue) and the selection
/// state-builder for the power catalog. Option-sets are reused across settings with shared loc keys,
/// so they live here rather than inlined per setting.</summary>
public static class PowerOptions
{
    public static readonly (string Label, int Value)[] AmdPowerSlider =
        new (string Label, int Value)[] { ("Template_AmdPowerSlider_Option_0", 0), ("Template_AmdPowerSlider_Option_1", 1), ("Template_AmdPowerSlider_Option_2", 2), ("Template_AmdPowerSlider_Option_3", 3) };

    public static readonly (string Label, int Value)[] AtiPowerPlay =
        new (string Label, int Value)[] { ("Template_AtiPowerPlay_Option_0", 0), ("Template_AtiPowerPlay_Option_1", 1), ("Template_AtiPowerPlay_Option_2", 2) };

    public static readonly (string Label, int Value)[] BatteryActions =
        new (string Label, int Value)[] { ("Template_BatteryActions_Option_0", 0), ("Template_BatteryActions_Option_1", 1), ("Template_BatteryActions_Option_2", 2), ("Template_BatteryActions_Option_3", 3) };

    public static readonly (string Label, int Value)[] CoolingPolicy =
        new (string Label, int Value)[] { ("Template_CoolingPolicy_Option_0", 0), ("Template_CoolingPolicy_Option_1", 1) };

    public static readonly (string Label, int Value)[] EnabledDisabled =
        new (string Label, int Value)[] { ("Template_EnabledDisabled_Option_0", 0), ("Template_EnabledDisabled_Option_1", 1) };

    public static readonly (string Label, int Value)[] IntelGraphics =
        new (string Label, int Value)[] { ("Template_IntelGraphics_Option_0", 0), ("Template_IntelGraphics_Option_1", 1), ("Template_IntelGraphics_Option_2", 2) };

    public static readonly (string Label, int Value)[] JavaScriptTimers =
        new (string Label, int Value)[] { ("Template_JavaScriptTimers_Option_0", 0), ("Template_JavaScriptTimers_Option_1", 1) };

    public static readonly (string Label, int Value)[] LidActions =
        new (string Label, int Value)[] { ("Template_LidActions_Option_0", 0), ("Template_LidActions_Option_1", 1), ("Template_LidActions_Option_2", 2), ("Template_LidActions_Option_3", 3) };

    public static readonly (string Label, int Value)[] MediaSharing =
        new (string Label, int Value)[] { ("Template_MediaSharing_Option_0", 0), ("Template_MediaSharing_Option_1", 1) };

    public static readonly (string Label, int Value)[] OnOff =
        new (string Label, int Value)[] { ("Template_OnOff_Option_0", 0), ("Template_OnOff_Option_1", 1) };

    public static readonly (string Label, int Value)[] PciExpress =
        new (string Label, int Value)[] { ("Template_PciExpress_Option_0", 0), ("Template_PciExpress_Option_1", 1), ("Template_PciExpress_Option_2", 2) };

    public static readonly (string Label, int Value)[] PerformanceDecreasePolicy =
        new (string Label, int Value)[] { ("Template_PerformanceDecreasePolicy_Option_0", 0), ("Template_PerformanceDecreasePolicy_Option_1", 1), ("Template_PerformanceDecreasePolicy_Option_2", 2) };

    public static readonly (string Label, int Value)[] PerformanceIncreasePolicy =
        new (string Label, int Value)[] { ("Template_PerformanceIncreasePolicy_Option_0", 0), ("Template_PerformanceIncreasePolicy_Option_1", 1), ("Template_PerformanceIncreasePolicy_Option_2", 2), ("Template_PerformanceIncreasePolicy_Option_3", 3) };

    public static readonly (string Label, int Value)[] PowerButtonActions =
        new (string Label, int Value)[] { ("Template_PowerButtonActions_Option_0", 0), ("Template_PowerButtonActions_Option_1", 1), ("Template_PowerButtonActions_Option_2", 2), ("Template_PowerButtonActions_Option_3", 3), ("Template_PowerButtonActions_Option_4", 4) };

    public static readonly (string Label, int Value)[] ProcessorBoostMode =
        new (string Label, int Value)[] { ("Template_ProcessorBoostMode_Option_0", 0), ("Template_ProcessorBoostMode_Option_1", 1), ("Template_ProcessorBoostMode_Option_2", 2), ("Template_ProcessorBoostMode_Option_3", 3), ("Template_ProcessorBoostMode_Option_4", 4), ("Template_ProcessorBoostMode_Option_5", 5), ("Template_ProcessorBoostMode_Option_6", 6) };

    public static readonly (string Label, int Value)[] Slideshow =
        new (string Label, int Value)[] { ("Template_Slideshow_Option_0", 0), ("Template_Slideshow_Option_1", 1) };

    public static readonly (string Label, int Value)[] SwitchableGraphics =
        new (string Label, int Value)[] { ("Template_SwitchableGraphics_Option_0", 0), ("Template_SwitchableGraphics_Option_1", 1), ("Template_SwitchableGraphics_Option_2", 2) };

    public static readonly (string Label, int Value)[] TimeIntervals =
        new (string Label, int Value)[] { ("Template_TimeIntervals_Option_0", 0), ("Template_TimeIntervals_Option_1", 60), ("Template_TimeIntervals_Option_2", 120), ("Template_TimeIntervals_Option_3", 180), ("Template_TimeIntervals_Option_4", 300), ("Template_TimeIntervals_Option_5", 600), ("Template_TimeIntervals_Option_6", 900), ("Template_TimeIntervals_Option_7", 1200), ("Template_TimeIntervals_Option_8", 1500), ("Template_TimeIntervals_Option_9", 1800), ("Template_TimeIntervals_Option_10", 2700), ("Template_TimeIntervals_Option_11", 3600), ("Template_TimeIntervals_Option_12", 7200), ("Template_TimeIntervals_Option_13", 10800), ("Template_TimeIntervals_Option_14", 14400), ("Template_TimeIntervals_Option_15", 18000) };

    public static readonly (string Label, int Value)[] Usb3LinkPower =
        new (string Label, int Value)[] { ("Template_Usb3LinkPower_Option_0", 0), ("Template_Usb3LinkPower_Option_1", 1), ("Template_Usb3LinkPower_Option_2", 2), ("Template_Usb3LinkPower_Option_3", 3) };

    public static readonly (string Label, int Value)[] VideoPlayback =
        new (string Label, int Value)[] { ("Template_VideoPlayback_Option_0", 0), ("Template_VideoPlayback_Option_1", 1), ("Template_VideoPlayback_Option_2", 2) };

    public static readonly (string Label, int Value)[] VideoQualityBias =
        new (string Label, int Value)[] { ("Template_VideoQualityBias_Option_0", 0), ("Template_VideoQualityBias_Option_1", 1) };

    public static readonly (string Label, int Value)[] WakeTimers =
        new (string Label, int Value)[] { ("Template_WakeTimers_Option_0", 0), ("Template_WakeTimers_Option_1", 1), ("Template_WakeTimers_Option_2", 2) };

    public static readonly (string Label, int Value)[] WirelessPower =
        new (string Label, int Value)[] { ("Template_WirelessPower_Option_0", 0), ("Template_WirelessPower_Option_1", 1), ("Template_WirelessPower_Option_2", 2), ("Template_WirelessPower_Option_3", 3) };

    /// <summary>Builds the per-option selection states for a powercfg selection, mirroring
    /// the retired SettingDefinitionConverter.ConvertPowerCfg: one state per option carrying the option's
    /// PowerCfgValue under the "Power" key, with context-scoped roles (Recommended/WindowsDefault per
    /// AC and DC) derived from the per-mode recommended/default VALUES, in a fixed role order.</summary>
    public static IReadOnlyList<SettingState> SelectionStates(
        (string Label, int Value)[] options, int? recAC, int? recDC, int? defAC, int? defDC,
        IReadOnlyList<Link>? links = null)
    {
        var states = new List<SettingState>(options.Length);
        foreach (var (label, value) in options)
        {
            var roles = new List<StateRole>();
            if (recAC == value) roles.Add(new StateRole(RoleKind.Recommended, PowerContext.AC));
            if (recDC == value) roles.Add(new StateRole(RoleKind.Recommended, PowerContext.DC));
            if (defAC == value) roles.Add(new StateRole(RoleKind.WindowsDefault, PowerContext.AC));
            if (defDC == value) roles.Add(new StateRole(RoleKind.WindowsDefault, PowerContext.DC));
            var state = new SettingState
            {
                Label = label,
                Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(value) },
                Roles = roles,
            };
            // Forward Links (Phase 6.6) ride on every non-WindowsDefault state, matching
            // the retired SettingDefinitionConverter.WithLinks (HasRole defaults to PowerContext.Always, so a
            // context-scoped WindowsDefault role does not suppress the link).
            if (links is { Count: > 0 } && !state.HasRole(RoleKind.WindowsDefault))
                state = state with { Links = links };
            states.Add(state);
        }
        return states;
    }
}

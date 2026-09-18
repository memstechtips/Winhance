using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Models;

public static class PowerOptions
{
    public static readonly (LocKey Label, int Value)[] AmdPowerSlider =
        new (LocKey Label, int Value)[] { (LocKey.Template.AmdPowerSliderOption0, 0), (LocKey.Template.AmdPowerSliderOption1, 1), (LocKey.Template.AmdPowerSliderOption2, 2), (LocKey.Template.AmdPowerSliderOption3, 3) };

    public static readonly (LocKey Label, int Value)[] AtiPowerPlay =
        new (LocKey Label, int Value)[] { (LocKey.Template.AtiPowerPlayOption0, 0), (LocKey.Template.AtiPowerPlayOption1, 1), (LocKey.Template.AtiPowerPlayOption2, 2) };

    public static readonly (LocKey Label, int Value)[] BatteryActions =
        new (LocKey Label, int Value)[] { (LocKey.Template.BatteryActionsOption0, 0), (LocKey.Template.BatteryActionsOption1, 1), (LocKey.Template.BatteryActionsOption2, 2), (LocKey.Template.BatteryActionsOption3, 3) };

    public static readonly (LocKey Label, int Value)[] CoolingPolicy =
        new (LocKey Label, int Value)[] { (LocKey.Template.CoolingPolicyOption0, 0), (LocKey.Template.CoolingPolicyOption1, 1) };

    // Deliberately NOT the Common_ pair: a two-option selection labelled with those would derive as a Toggle,
    // which a powercfg setting must never do.
    public static readonly (LocKey Label, int Value)[] EnabledDisabled =
        new (LocKey Label, int Value)[] { (LocKey.Template.EnabledDisabledOption0, 0), (LocKey.Template.EnabledDisabledOption1, 1) };

    public static readonly (LocKey Label, int Value)[] IntelGraphics =
        new (LocKey Label, int Value)[] { (LocKey.Template.IntelGraphicsOption0, 0), (LocKey.Template.IntelGraphicsOption1, 1), (LocKey.Template.IntelGraphicsOption2, 2) };

    public static readonly (LocKey Label, int Value)[] JavaScriptTimers =
        new (LocKey Label, int Value)[] { (LocKey.Template.JavaScriptTimersOption0, 0), (LocKey.Template.JavaScriptTimersOption1, 1) };

    public static readonly (LocKey Label, int Value)[] LidActions =
        new (LocKey Label, int Value)[] { (LocKey.Template.LidActionsOption0, 0), (LocKey.Template.LidActionsOption1, 1), (LocKey.Template.LidActionsOption2, 2), (LocKey.Template.LidActionsOption3, 3) };

    public static readonly (LocKey Label, int Value)[] MediaSharing =
        new (LocKey Label, int Value)[] { (LocKey.Template.MediaSharingOption0, 0), (LocKey.Template.MediaSharingOption1, 1) };

    public static readonly (LocKey Label, int Value)[] OnOff =
        new (LocKey Label, int Value)[] { (LocKey.Template.OnOffOption0, 0), (LocKey.Template.OnOffOption1, 1) };

    public static readonly (LocKey Label, int Value)[] PciExpress =
        new (LocKey Label, int Value)[] { (LocKey.Template.PciExpressOption0, 0), (LocKey.Template.PciExpressOption1, 1), (LocKey.Template.PciExpressOption2, 2) };

    public static readonly (LocKey Label, int Value)[] PerformanceDecreasePolicy =
        new (LocKey Label, int Value)[] { (LocKey.Template.PerformanceDecreasePolicyOption0, 0), (LocKey.Template.PerformanceDecreasePolicyOption1, 1), (LocKey.Template.PerformanceDecreasePolicyOption2, 2) };

    public static readonly (LocKey Label, int Value)[] PerformanceIncreasePolicy =
        new (LocKey Label, int Value)[] { (LocKey.Template.PerformanceIncreasePolicyOption0, 0), (LocKey.Template.PerformanceIncreasePolicyOption1, 1), (LocKey.Template.PerformanceIncreasePolicyOption2, 2), (LocKey.Template.PerformanceIncreasePolicyOption3, 3) };

    public static readonly (LocKey Label, int Value)[] PowerButtonActions =
        new (LocKey Label, int Value)[] { (LocKey.Template.PowerButtonActionsOption0, 0), (LocKey.Template.PowerButtonActionsOption1, 1), (LocKey.Template.PowerButtonActionsOption2, 2), (LocKey.Template.PowerButtonActionsOption3, 3), (LocKey.Template.PowerButtonActionsOption4, 4) };

    public static readonly (LocKey Label, int Value)[] ProcessorBoostMode =
        new (LocKey Label, int Value)[] { (LocKey.Template.ProcessorBoostModeOption0, 0), (LocKey.Template.ProcessorBoostModeOption1, 1), (LocKey.Template.ProcessorBoostModeOption2, 2), (LocKey.Template.ProcessorBoostModeOption3, 3), (LocKey.Template.ProcessorBoostModeOption4, 4), (LocKey.Template.ProcessorBoostModeOption5, 5), (LocKey.Template.ProcessorBoostModeOption6, 6) };

    public static readonly (LocKey Label, int Value)[] Slideshow =
        new (LocKey Label, int Value)[] { (LocKey.Template.SlideshowOption0, 0), (LocKey.Template.SlideshowOption1, 1) };

    public static readonly (LocKey Label, int Value)[] SwitchableGraphics =
        new (LocKey Label, int Value)[] { (LocKey.Template.SwitchableGraphicsOption0, 0), (LocKey.Template.SwitchableGraphicsOption1, 1), (LocKey.Template.SwitchableGraphicsOption2, 2) };

    public static readonly (LocKey Label, int Value)[] TimeIntervals =
        new (LocKey Label, int Value)[] { (LocKey.Template.TimeIntervalsOption0, 0), (LocKey.Template.TimeIntervalsOption1, 60), (LocKey.Template.TimeIntervalsOption2, 120), (LocKey.Template.TimeIntervalsOption3, 180), (LocKey.Template.TimeIntervalsOption4, 300), (LocKey.Template.TimeIntervalsOption5, 600), (LocKey.Template.TimeIntervalsOption6, 900), (LocKey.Template.TimeIntervalsOption7, 1200), (LocKey.Template.TimeIntervalsOption8, 1500), (LocKey.Template.TimeIntervalsOption9, 1800), (LocKey.Template.TimeIntervalsOption10, 2700), (LocKey.Template.TimeIntervalsOption11, 3600), (LocKey.Template.TimeIntervalsOption12, 7200), (LocKey.Template.TimeIntervalsOption13, 10800), (LocKey.Template.TimeIntervalsOption14, 14400), (LocKey.Template.TimeIntervalsOption15, 18000) };

    public static readonly (LocKey Label, int Value)[] Usb3LinkPower =
        new (LocKey Label, int Value)[] { (LocKey.Template.Usb3LinkPowerOption0, 0), (LocKey.Template.Usb3LinkPowerOption1, 1), (LocKey.Template.Usb3LinkPowerOption2, 2), (LocKey.Template.Usb3LinkPowerOption3, 3) };

    public static readonly (LocKey Label, int Value)[] VideoPlayback =
        new (LocKey Label, int Value)[] { (LocKey.Template.VideoPlaybackOption0, 0), (LocKey.Template.VideoPlaybackOption1, 1), (LocKey.Template.VideoPlaybackOption2, 2) };

    public static readonly (LocKey Label, int Value)[] VideoQualityBias =
        new (LocKey Label, int Value)[] { (LocKey.Template.VideoQualityBiasOption0, 0), (LocKey.Template.VideoQualityBiasOption1, 1) };

    public static readonly (LocKey Label, int Value)[] WakeTimers =
        new (LocKey Label, int Value)[] { (LocKey.Template.WakeTimersOption0, 0), (LocKey.Template.WakeTimersOption1, 1), (LocKey.Template.WakeTimersOption2, 2) };

    public static readonly (LocKey Label, int Value)[] WirelessPower =
        new (LocKey Label, int Value)[] { (LocKey.Template.WirelessPowerOption0, 0), (LocKey.Template.WirelessPowerOption1, 1), (LocKey.Template.WirelessPowerOption2, 2), (LocKey.Template.WirelessPowerOption3, 3) };

    public static IReadOnlyList<SettingState> SelectionStates(
        (LocKey Label, int Value)[] options, int? recAC, int? recDC, int? defAC, int? defDC,
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
            // Forward Links ride on every non-WindowsDefault state (HasRole defaults to PowerContext.Always,
            // so a context-scoped WindowsDefault role does not suppress the link).
            if (links is { Count: > 0 } && !state.HasRole(RoleKind.WindowsDefault))
                state = state with { Links = links };
            states.Add(state);
        }
        return states;
    }
}

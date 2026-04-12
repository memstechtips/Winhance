using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

// Flags intentionally omitted on all templates below — every PowerTemplates consumer in
// PowerOptimizations.cs is a PowerCfg-backed Selection whose Recommended/Default state lives on
// PowerRecommendation (per-mode AC/DC) + PowerCfgSetting.RecommendedValueAC/DC / DefaultValueAC/DC,
// not on ComboBoxOption.IsRecommended / IsDefault. Single-flag options can't encode distinct
// AC/DC recommendations; see SettingCatalogValidatorTests for the PowerCfg-backed exemption.
public static class PowerTemplates
{
    public static readonly ComboBoxMetadata TimeIntervals = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 60 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 120 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 180 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_4",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 300 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_5",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 600 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_6",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 900 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_7",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1200 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_8",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1500 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_9",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1800 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_10",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2700 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_11",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3600 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_12",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 7200 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_13",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 10800 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_14",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 14400 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_TimeIntervals_Option_15",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 18000 },
            },
        },
    };

    public static readonly ComboBoxMetadata OnOff = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_OnOff_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_OnOff_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata EnabledDisabled = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_EnabledDisabled_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_EnabledDisabled_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata WakeTimers = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_WakeTimers_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_WakeTimers_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_WakeTimers_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata PowerButtonActions = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_PowerButtonActions_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PowerButtonActions_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PowerButtonActions_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PowerButtonActions_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PowerButtonActions_Option_4",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 },
            },
        },
    };

    public static readonly ComboBoxMetadata LidActions = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_LidActions_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_LidActions_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_LidActions_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_LidActions_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata CoolingPolicy = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_CoolingPolicy_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_CoolingPolicy_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata BatteryActions = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_BatteryActions_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_BatteryActions_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_BatteryActions_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_BatteryActions_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata WirelessPower = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_WirelessPower_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_WirelessPower_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_WirelessPower_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_WirelessPower_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata Slideshow = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_Slideshow_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_Slideshow_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata PciExpress = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_PciExpress_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PciExpress_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PciExpress_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata Usb3LinkPower = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_Usb3LinkPower_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_Usb3LinkPower_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_Usb3LinkPower_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_Usb3LinkPower_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata MediaSharing = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_MediaSharing_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_MediaSharing_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata VideoQualityBias = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_VideoQualityBias_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_VideoQualityBias_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata VideoPlayback = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_VideoPlayback_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_VideoPlayback_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_VideoPlayback_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata AmdPowerSlider = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_AmdPowerSlider_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_AmdPowerSlider_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_AmdPowerSlider_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_AmdPowerSlider_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata JavaScriptTimers = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_JavaScriptTimers_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_JavaScriptTimers_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata IntelGraphics = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_IntelGraphics_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_IntelGraphics_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_IntelGraphics_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata AtiPowerPlay = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_AtiPowerPlay_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_AtiPowerPlay_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_AtiPowerPlay_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata SwitchableGraphics = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_SwitchableGraphics_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_SwitchableGraphics_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_SwitchableGraphics_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata ProcessorBoostMode = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_4",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_5",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 5 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_ProcessorBoostMode_Option_6",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 6 },
            },
        },
    };

    public static readonly ComboBoxMetadata PerformanceIncreasePolicy = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceIncreasePolicy_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceIncreasePolicy_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceIncreasePolicy_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceIncreasePolicy_Option_3",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata PerformanceDecreasePolicy = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceDecreasePolicy_Option_0",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceDecreasePolicy_Option_1",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Template_PerformanceDecreasePolicy_Option_2",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static NumericRangeMetadata CreateNumericRange(int minValue, int maxValue, string units)
    {
        return new NumericRangeMetadata
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Increment = 1,
            Units = units
        };
    }
}

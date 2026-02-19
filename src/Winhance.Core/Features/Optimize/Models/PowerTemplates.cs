using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.Optimize.Models
{
    public static class PowerTemplates
    {
        public static readonly Dictionary<string, object> TimeIntervals = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
            {
                "Template_TimeIntervals_Option_0",
                "Template_TimeIntervals_Option_1",
                "Template_TimeIntervals_Option_2",
                "Template_TimeIntervals_Option_3",
                "Template_TimeIntervals_Option_4",
                "Template_TimeIntervals_Option_5",
                "Template_TimeIntervals_Option_6",
                "Template_TimeIntervals_Option_7",
                "Template_TimeIntervals_Option_8",
                "Template_TimeIntervals_Option_9",
                "Template_TimeIntervals_Option_10",
                "Template_TimeIntervals_Option_11",
                "Template_TimeIntervals_Option_12",
                "Template_TimeIntervals_Option_13",
                "Template_TimeIntervals_Option_14",
                "Template_TimeIntervals_Option_15"
            },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 60 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 120 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 180 },
                [4] = new Dictionary<string, object?> { ["PowerCfgValue"] = 300 },
                [5] = new Dictionary<string, object?> { ["PowerCfgValue"] = 600 },
                [6] = new Dictionary<string, object?> { ["PowerCfgValue"] = 900 },
                [7] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1200 },
                [8] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1500 },
                [9] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1800 },
                [10] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2700 },
                [11] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3600 },
                [12] = new Dictionary<string, object?> { ["PowerCfgValue"] = 7200 },
                [13] = new Dictionary<string, object?> { ["PowerCfgValue"] = 10800 },
                [14] = new Dictionary<string, object?> { ["PowerCfgValue"] = 14400 },
                [15] = new Dictionary<string, object?> { ["PowerCfgValue"] = 18000 }
            }
        };

        public static readonly Dictionary<string, object> OnOff = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_OnOff_Option_0", "Template_OnOff_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["Value"] = 0 },
                [1] = new Dictionary<string, object?> { ["Value"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> EnabledDisabled = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_EnabledDisabled_Option_0", "Template_EnabledDisabled_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["Value"] = 0 },
                [1] = new Dictionary<string, object?> { ["Value"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> WakeTimers = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_WakeTimers_Option_0", "Template_WakeTimers_Option_1", "Template_WakeTimers_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly Dictionary<string, object> PowerButtonActions = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_PowerButtonActions_Option_0", "Template_PowerButtonActions_Option_1", "Template_PowerButtonActions_Option_2", "Template_PowerButtonActions_Option_3", "Template_PowerButtonActions_Option_4" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
                [4] = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 }
            }
        };

        public static readonly Dictionary<string, object> LidActions = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_LidActions_Option_0", "Template_LidActions_Option_1", "Template_LidActions_Option_2", "Template_LidActions_Option_3" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly Dictionary<string, object> CoolingPolicy = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_CoolingPolicy_Option_0", "Template_CoolingPolicy_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> BatteryActions = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_BatteryActions_Option_0", "Template_BatteryActions_Option_1", "Template_BatteryActions_Option_2", "Template_BatteryActions_Option_3" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly Dictionary<string, object> WirelessPower = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_WirelessPower_Option_0", "Template_WirelessPower_Option_1", "Template_WirelessPower_Option_2", "Template_WirelessPower_Option_3" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly Dictionary<string, object> Slideshow = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_Slideshow_Option_0", "Template_Slideshow_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> PciExpress = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_PciExpress_Option_0", "Template_PciExpress_Option_1", "Template_PciExpress_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly Dictionary<string, object> Usb3LinkPower = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_Usb3LinkPower_Option_0", "Template_Usb3LinkPower_Option_1", "Template_Usb3LinkPower_Option_2", "Template_Usb3LinkPower_Option_3" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly Dictionary<string, object> MediaSharing = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_MediaSharing_Option_0", "Template_MediaSharing_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> VideoQualityBias = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_VideoQualityBias_Option_0", "Template_VideoQualityBias_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> VideoPlayback = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_VideoPlayback_Option_0", "Template_VideoPlayback_Option_1", "Template_VideoPlayback_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly Dictionary<string, object> AmdPowerSlider = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_AmdPowerSlider_Option_0", "Template_AmdPowerSlider_Option_1", "Template_AmdPowerSlider_Option_2", "Template_AmdPowerSlider_Option_3" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly Dictionary<string, object> JavaScriptTimers = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_JavaScriptTimers_Option_0", "Template_JavaScriptTimers_Option_1" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly Dictionary<string, object> IntelGraphics = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_IntelGraphics_Option_0", "Template_IntelGraphics_Option_1", "Template_IntelGraphics_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly Dictionary<string, object> AtiPowerPlay = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_AtiPowerPlay_Option_0", "Template_AtiPowerPlay_Option_1", "Template_AtiPowerPlay_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly Dictionary<string, object> SwitchableGraphics = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_SwitchableGraphics_Option_0", "Template_SwitchableGraphics_Option_1", "Template_SwitchableGraphics_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly Dictionary<string, object> ProcessorBoostMode = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_ProcessorBoostMode_Option_0", "Template_ProcessorBoostMode_Option_1", "Template_ProcessorBoostMode_Option_2", "Template_ProcessorBoostMode_Option_3", "Template_ProcessorBoostMode_Option_4", "Template_ProcessorBoostMode_Option_5", "Template_ProcessorBoostMode_Option_6" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
                [4] = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 },
                [5] = new Dictionary<string, object?> { ["PowerCfgValue"] = 5 },
                [6] = new Dictionary<string, object?> { ["PowerCfgValue"] = 6 }
            }
        };

        public static readonly Dictionary<string, object> PerformanceIncreasePolicy = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_PerformanceIncreasePolicy_Option_0", "Template_PerformanceIncreasePolicy_Option_1", "Template_PerformanceIncreasePolicy_Option_2", "Template_PerformanceIncreasePolicy_Option_3" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly Dictionary<string, object> PerformanceDecreasePolicy = new()
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_PerformanceDecreasePolicy_Option_0", "Template_PerformanceDecreasePolicy_Option_1", "Template_PerformanceDecreasePolicy_Option_2" },
            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static Dictionary<string, object> CreateNumericRange(int minValue, int maxValue, string units)
        {
            return new Dictionary<string, object>
            {
                ["MinValue"] = minValue,
                ["MaxValue"] = maxValue,
                ["Increment"] = 1,
                ["Units"] = units
            };
        }
    }
}

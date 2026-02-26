using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    public static class PowerTemplates
    {
        public static readonly ComboBoxMetadata TimeIntervals = new()
        {
            DisplayNames = new string[]
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
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
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

        public static readonly ComboBoxMetadata OnOff = new()
        {
            DisplayNames = new string[] { "Template_OnOff_Option_0", "Template_OnOff_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["Value"] = 0 },
                [1] = new Dictionary<string, object?> { ["Value"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata EnabledDisabled = new()
        {
            DisplayNames = new string[] { "Template_EnabledDisabled_Option_0", "Template_EnabledDisabled_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["Value"] = 0 },
                [1] = new Dictionary<string, object?> { ["Value"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata WakeTimers = new()
        {
            DisplayNames = new string[] { "Template_WakeTimers_Option_0", "Template_WakeTimers_Option_1", "Template_WakeTimers_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly ComboBoxMetadata PowerButtonActions = new()
        {
            DisplayNames = new string[] { "Template_PowerButtonActions_Option_0", "Template_PowerButtonActions_Option_1", "Template_PowerButtonActions_Option_2", "Template_PowerButtonActions_Option_3", "Template_PowerButtonActions_Option_4" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
                [4] = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 }
            }
        };

        public static readonly ComboBoxMetadata LidActions = new()
        {
            DisplayNames = new string[] { "Template_LidActions_Option_0", "Template_LidActions_Option_1", "Template_LidActions_Option_2", "Template_LidActions_Option_3" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly ComboBoxMetadata CoolingPolicy = new()
        {
            DisplayNames = new string[] { "Template_CoolingPolicy_Option_0", "Template_CoolingPolicy_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata BatteryActions = new()
        {
            DisplayNames = new string[] { "Template_BatteryActions_Option_0", "Template_BatteryActions_Option_1", "Template_BatteryActions_Option_2", "Template_BatteryActions_Option_3" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly ComboBoxMetadata WirelessPower = new()
        {
            DisplayNames = new string[] { "Template_WirelessPower_Option_0", "Template_WirelessPower_Option_1", "Template_WirelessPower_Option_2", "Template_WirelessPower_Option_3" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly ComboBoxMetadata Slideshow = new()
        {
            DisplayNames = new string[] { "Template_Slideshow_Option_0", "Template_Slideshow_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata PciExpress = new()
        {
            DisplayNames = new string[] { "Template_PciExpress_Option_0", "Template_PciExpress_Option_1", "Template_PciExpress_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly ComboBoxMetadata Usb3LinkPower = new()
        {
            DisplayNames = new string[] { "Template_Usb3LinkPower_Option_0", "Template_Usb3LinkPower_Option_1", "Template_Usb3LinkPower_Option_2", "Template_Usb3LinkPower_Option_3" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly ComboBoxMetadata MediaSharing = new()
        {
            DisplayNames = new string[] { "Template_MediaSharing_Option_0", "Template_MediaSharing_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata VideoQualityBias = new()
        {
            DisplayNames = new string[] { "Template_VideoQualityBias_Option_0", "Template_VideoQualityBias_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata VideoPlayback = new()
        {
            DisplayNames = new string[] { "Template_VideoPlayback_Option_0", "Template_VideoPlayback_Option_1", "Template_VideoPlayback_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly ComboBoxMetadata AmdPowerSlider = new()
        {
            DisplayNames = new string[] { "Template_AmdPowerSlider_Option_0", "Template_AmdPowerSlider_Option_1", "Template_AmdPowerSlider_Option_2", "Template_AmdPowerSlider_Option_3" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly ComboBoxMetadata JavaScriptTimers = new()
        {
            DisplayNames = new string[] { "Template_JavaScriptTimers_Option_0", "Template_JavaScriptTimers_Option_1" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }
            }
        };

        public static readonly ComboBoxMetadata IntelGraphics = new()
        {
            DisplayNames = new string[] { "Template_IntelGraphics_Option_0", "Template_IntelGraphics_Option_1", "Template_IntelGraphics_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly ComboBoxMetadata AtiPowerPlay = new()
        {
            DisplayNames = new string[] { "Template_AtiPowerPlay_Option_0", "Template_AtiPowerPlay_Option_1", "Template_AtiPowerPlay_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly ComboBoxMetadata SwitchableGraphics = new()
        {
            DisplayNames = new string[] { "Template_SwitchableGraphics_Option_0", "Template_SwitchableGraphics_Option_1", "Template_SwitchableGraphics_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
        };

        public static readonly ComboBoxMetadata ProcessorBoostMode = new()
        {
            DisplayNames = new string[] { "Template_ProcessorBoostMode_Option_0", "Template_ProcessorBoostMode_Option_1", "Template_ProcessorBoostMode_Option_2", "Template_ProcessorBoostMode_Option_3", "Template_ProcessorBoostMode_Option_4", "Template_ProcessorBoostMode_Option_5", "Template_ProcessorBoostMode_Option_6" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
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

        public static readonly ComboBoxMetadata PerformanceIncreasePolicy = new()
        {
            DisplayNames = new string[] { "Template_PerformanceIncreasePolicy_Option_0", "Template_PerformanceIncreasePolicy_Option_1", "Template_PerformanceIncreasePolicy_Option_2", "Template_PerformanceIncreasePolicy_Option_3" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
                [3] = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 }
            }
        };

        public static readonly ComboBoxMetadata PerformanceDecreasePolicy = new()
        {
            DisplayNames = new string[] { "Template_PerformanceDecreasePolicy_Option_0", "Template_PerformanceDecreasePolicy_Option_1", "Template_PerformanceDecreasePolicy_Option_2" },
            ValueMappings = new Dictionary<int, Dictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
                [1] = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
                [2] = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 }
            }
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
}

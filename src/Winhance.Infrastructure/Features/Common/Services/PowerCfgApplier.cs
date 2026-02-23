using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class PowerCfgApplier(
        IPowerSettingsQueryService powerSettingsQueryService,
        IHardwareDetectionService hardwareDetectionService,
        IComboBoxResolver comboBoxResolver,
        ILogService logService) : IPowerCfgApplier
    {
        public async Task ApplyPowerCfgSettingsAsync(SettingDefinition setting, bool enable, object? value)
        {
            if (setting.PowerCfgSettings == null || setting.PowerCfgSettings.Count == 0)
                return;

            if (setting.InputType == InputType.Selection &&
                setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                value is ValueTuple<int, int> tupleSeparate)
            {
                logService.Log(LogLevel.Info, $"[PowerCfgApplier] Applying PowerCfg settings for '{setting.Id}' with separate AC/DC Selection tuple");

                var acPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, tupleSeparate.Item1);
                var dcPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, tupleSeparate.Item2);

                var convertedDict = new Dictionary<string, object?>
                {
                    ["ACValue"] = acPowerCfgValue,
                    ["DCValue"] = dcPowerCfgValue
                };

                await ExecutePowerCfgSettings(setting.PowerCfgSettings, convertedDict, await hardwareDetectionService.HasBatteryAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
            else if (setting.InputType == InputType.Selection &&
                setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                value is Dictionary<string, object?> dict)
            {
                logService.Log(LogLevel.Info, $"[PowerCfgApplier] Applying PowerCfg settings for '{setting.Id}' with separate AC/DC Selection values");

                var acIndex = ExtractIndexFromValue(dict.TryGetValue("ACValue", out var acVal) ? acVal : 0);
                var dcIndex = ExtractIndexFromValue(dict.TryGetValue("DCValue", out var dcVal) ? dcVal : 0);

                var acPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, acIndex);
                var dcPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, dcIndex);

                var convertedDict = new Dictionary<string, object?>
                {
                    ["ACValue"] = acPowerCfgValue,
                    ["DCValue"] = dcPowerCfgValue
                };

                await ExecutePowerCfgSettings(setting.PowerCfgSettings, convertedDict, await hardwareDetectionService.HasBatteryAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
            else if (setting.InputType == InputType.NumericRange &&
                     setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                     value is Dictionary<string, object?> numericDict)
            {
                logService.Log(LogLevel.Info, $"[PowerCfgApplier] Applying PowerCfg settings for '{setting.Id}' with separate AC/DC NumericRange values");

                var acValue = numericDict.TryGetValue("ACValue", out var ac) ? ExtractSingleValue(ac) : 0;
                var dcValue = numericDict.TryGetValue("DCValue", out var dc) ? ExtractSingleValue(dc) : 0;

                var acSystemValue = ConvertToSystemUnits(acValue, setting.PowerCfgSettings[0].Units);
                var dcSystemValue = ConvertToSystemUnits(dcValue, setting.PowerCfgSettings[0].Units);

                var convertedDict = new Dictionary<string, object?>
                {
                    ["ACValue"] = acSystemValue,
                    ["DCValue"] = dcSystemValue
                };

                await ExecutePowerCfgSettings(setting.PowerCfgSettings, convertedDict, await hardwareDetectionService.HasBatteryAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
            else
            {
                if (setting.InputType == InputType.NumericRange && value == null)
                {
                    logService.Log(LogLevel.Debug, $"[PowerCfgApplier] Skipping PowerCfg setting '{setting.Id}' - no value provided (old config format)");
                    return;
                }

                int valueToApply = setting.InputType switch
                {
                    InputType.Toggle => enable ? 1 : 0,
                    InputType.Selection when value is int index => comboBoxResolver.GetValueFromIndex(setting, index),
                    InputType.NumericRange when value != null => ConvertToSystemUnits(ConvertNumericValue(value), GetDisplayUnits(setting)),
                    _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for PowerCfg operations")
                };

                logService.Log(LogLevel.Info, $"[PowerCfgApplier] Applying {setting.PowerCfgSettings.Count} PowerCfg settings for '{setting.Id}' with value: {valueToApply}");
                await ExecutePowerCfgSettings(setting.PowerCfgSettings, valueToApply, await hardwareDetectionService.HasBatteryAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        private async Task ExecutePowerCfgSettings(List<PowerCfgSetting> powerCfgSettings, object valueToApply, bool hasBattery = true)
        {
            var activeSchemeResult = PowerProf.PowerGetActiveScheme(IntPtr.Zero, out var activeSchemePtr);
            if (activeSchemeResult != PowerProf.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Error, "[PowerCfgApplier] Failed to get active power scheme");
                return;
            }

            var activeSchemeGuid = Marshal.PtrToStructure<Guid>(activeSchemePtr);
            PowerProf.LocalFree(activeSchemePtr);

            int changeCount = 0;

            foreach (var powerCfgSetting in powerCfgSettings)
            {
                var (currentAc, currentDc) = await powerSettingsQueryService.GetPowerSettingACDCValuesAsync(powerCfgSetting).ConfigureAwait(false);
                var subgroupGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                var settingGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                switch (powerCfgSetting.PowerModeSupport)
                {
                    case PowerModeSupport.Both:
                        var singleValue = ExtractSingleValue(valueToApply);

                        if (currentAc != singleValue)
                        {
                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)singleValue);
                            changeCount++;
                        }

                        if (hasBattery && currentDc != singleValue)
                        {
                            PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)singleValue);
                            changeCount++;
                        }
                        break;

                    case PowerModeSupport.Separate:
                        var (acValue, dcValue) = ExtractACDCValues(valueToApply);

                        if (currentAc != acValue)
                        {
                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)acValue);
                            changeCount++;
                        }

                        if (hasBattery && currentDc != dcValue)
                        {
                            PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)dcValue);
                            changeCount++;
                        }
                        break;

                    case PowerModeSupport.ACOnly:
                        var acOnlyValue = ExtractSingleValue(valueToApply);
                        if (currentAc != acOnlyValue)
                        {
                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)acOnlyValue);
                            changeCount++;
                        }
                        break;

                    case PowerModeSupport.DCOnly:
                        if (hasBattery)
                        {
                            var dcOnlyValue = ExtractSingleValue(valueToApply);
                            if (currentDc != dcOnlyValue)
                            {
                                PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)dcOnlyValue);
                                changeCount++;
                            }
                        }
                        break;
                }
            }

            if (changeCount == 0)
            {
                logService.Log(LogLevel.Info, "[PowerCfgApplier] No powercfg changes needed (values already match)");
                return;
            }

            PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref activeSchemeGuid);

            logService.Log(LogLevel.Info, $"[PowerCfgApplier] Applied {changeCount} powercfg changes via P/Invoke");
        }

        private int ConvertNumericValue(object value)
        {
            return value switch
            {
                int intVal => intVal,
                long longVal => (int)longVal,
                double doubleVal => (int)doubleVal,
                float floatVal => (int)floatVal,
                string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                System.Text.Json.JsonElement je when je.TryGetInt32(out int jsonInt) => jsonInt,
                _ => throw new ArgumentException($"Cannot convert '{value}' (type: {value?.GetType().Name ?? "null"}) to numeric value")
            };
        }

        private int ConvertToSystemUnits(int displayValue, string? units)
        {
            return units?.ToLowerInvariant() switch
            {
                "minutes" => displayValue * 60,
                "hours" => displayValue * 3600,
                "milliseconds" => displayValue / 1000,
                _ => displayValue
            };
        }

        private string GetDisplayUnits(SettingDefinition setting)
        {
            if (setting.CustomProperties?.TryGetValue("Units", out var units) == true && units is string unitsStr)
                return unitsStr;
            return setting.PowerCfgSettings?[0]?.Units ?? string.Empty;
        }

        private int ExtractSingleValue(object? value)
        {
            return value switch
            {
                int intVal => intVal,
                long longVal => (int)longVal,
                double doubleVal => (int)doubleVal,
                float floatVal => (int)floatVal,
                string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                ValueTuple<int, int> tuple => tuple.Item1,
                System.Text.Json.JsonElement je when je.TryGetInt32(out int jsonInt) => jsonInt,
                _ => throw new ArgumentException($"Cannot convert '{value}' (type: {value?.GetType().Name ?? "null"}) to single numeric value")
            };
        }

        private (int acValue, int dcValue) ExtractACDCValues(object value)
        {
            if (value is ValueTuple<object, object> tuple)
            {
                return (ExtractSingleValue(tuple.Item1), ExtractSingleValue(tuple.Item2));
            }

            if (value is Dictionary<string, object?> dict)
            {
                var acValue = dict.TryGetValue("ACValue", out var ac) ? ExtractSingleValue(ac) : 0;
                var dcValue = dict.TryGetValue("DCValue", out var dc) ? ExtractSingleValue(dc) : 0;
                return (acValue, dcValue);
            }

            var singleValue = ExtractSingleValue(value);
            return (singleValue, singleValue);
        }

        private int ExtractIndexFromValue(object? value)
        {
            if (value == null) return 0;

            if (value.GetType().Name == "ComboBoxOption")
            {
                var valueProp = value.GetType().GetProperty("Value");
                if (valueProp != null)
                {
                    var innerValue = valueProp.GetValue(value);
                    if (innerValue is int intVal)
                        return intVal;
                }
            }

            if (value is int directInt)
                return directInt;

            if (value is System.Text.Json.JsonElement je)
                return je.TryGetInt32(out int jsonInt) ? jsonInt : 0;

            if (int.TryParse(value.ToString(), out int parsed))
                return parsed;

            return 0;
        }
    }
}

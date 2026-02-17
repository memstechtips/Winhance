using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class TooltipDataService(
        IWindowsRegistryService windowsRegistryService,
        ILogService logService) : ITooltipDataService
    {
        private readonly IWindowsRegistryService _registryService = windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
        private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        private static string FormatRegistryValue(object? value, RegistrySetting? registrySetting)
        {
            if (value == null)
                return "(not set)";

            if (value is byte[] bytes && registrySetting != null)
            {
                if (bytes.Length == 0)
                    return "(empty)";

                if (registrySetting.BinaryByteIndex.HasValue && bytes.Length > registrySetting.BinaryByteIndex.Value)
                {
                    var targetByte = bytes[registrySetting.BinaryByteIndex.Value];

                    if (registrySetting.BitMask.HasValue)
                    {
                        var isSet = (targetByte & registrySetting.BitMask.Value) != 0;
                        return isSet ? "1" : "0";
                    }

                    return targetByte.ToString();
                }

                return string.Join(" ", bytes);
            }

            return value.ToString() ?? "(not set)";
        }

        public async Task<Dictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<SettingDefinition> settings)
        {
            var tooltipData = new Dictionary<string, SettingTooltipData>();

            try
            {
                foreach (var setting in settings)
                {
                    var data = await GetTooltipDataForSettingAsync(setting);
                    if (data != null)
                    {
                        tooltipData[setting.Id] = data;
                    }
                }
            }
            catch
            {
                // Silent failure for bulk operations
            }

            return tooltipData;
        }

        public async Task<SettingTooltipData?> RefreshTooltipDataAsync(string settingId, SettingDefinition setting)
        {
            try
            {
                return await GetTooltipDataForSettingAsync(setting);
            }
            catch
            {
                return null;
            }
        }

        public async Task<Dictionary<string, SettingTooltipData>> RefreshMultipleTooltipDataAsync(IEnumerable<SettingDefinition> settings)
        {
            var tooltipData = new Dictionary<string, SettingTooltipData>();

            try
            {
                foreach (var setting in settings)
                {
                    var data = await GetTooltipDataForSettingAsync(setting);
                    if (data != null)
                    {
                        tooltipData[setting.Id] = data;
                    }
                }
            }
            catch
            {
                // Silent failure for bulk operations
            }

            return tooltipData;
        }

        private async Task<SettingTooltipData?> GetTooltipDataForSettingAsync(SettingDefinition setting)
        {
            if (setting.CustomProperties?.TryGetValue(CustomPropertyKeys.DisableTooltip, out var disableTooltipObj) == true
                && disableTooltipObj is bool disableTooltip
                && disableTooltip)
            {
                return null;
            }

            bool hasRegistrySettings = setting.RegistrySettings?.Any() == true;
            bool hasScheduledTaskSettings = setting.ScheduledTaskSettings?.Any() == true;
            bool hasPowerCfgSettings = setting.PowerCfgSettings?.Any() == true;

            if (!hasRegistrySettings && !hasScheduledTaskSettings && !hasPowerCfgSettings)
                return null;

            try
            {
                var tooltipData = new SettingTooltipData
                {
                    SettingId = setting.Id,
                    ScheduledTaskSettings = setting.ScheduledTaskSettings?.ToList() ?? new List<ScheduledTaskSetting>(),
                    PowerCfgSettings = setting.PowerCfgSettings?.ToList() ?? new List<PowerCfgSetting>()
                };

                if (hasRegistrySettings)
                {
                    var registrySettings = setting.RegistrySettings.ToList();
                    var individualValues = new Dictionary<RegistrySetting, string?>();
                    var primaryRegistrySetting = registrySettings.First();
                    string primaryDisplayValue = "(not set)";

                    foreach (var registrySetting in registrySettings)
                    {
                        try
                        {
                            object? currentValue;
                            if (registrySetting.ApplyPerNetworkInterface)
                            {
                                // Read from the first interface subkey as a representative value
                                var subKeys = _registryService.GetSubKeyNames(registrySetting.KeyPath);
                                if (subKeys.Length > 0)
                                {
                                    currentValue = _registryService.GetValue(
                                        $@"{registrySetting.KeyPath}\{subKeys[0]}",
                                        registrySetting.ValueName);
                                }
                                else
                                {
                                    currentValue = null;
                                }
                            }
                            else
                            {
                                currentValue = _registryService.GetValue(registrySetting.KeyPath, registrySetting.ValueName);
                            }
                            var formattedValue = FormatRegistryValue(currentValue, registrySetting);
                            individualValues[registrySetting] = formattedValue;

                            if (registrySetting == primaryRegistrySetting)
                            {
                                primaryDisplayValue = formattedValue;
                            }
                        }
                        catch
                        {
                            individualValues[registrySetting] = null;
                        }
                    }

                    tooltipData.RegistrySetting = primaryRegistrySetting;
                    tooltipData.DisplayValue = primaryDisplayValue;
                    tooltipData.IndividualRegistryValues = individualValues;
                }

                return tooltipData;
            }
            catch
            {
                return null;
            }
        }
    }
}

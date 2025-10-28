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
            bool hasCommandSettings = setting.CommandSettings?.Any() == true;
            bool hasPowerCfgSettings = setting.PowerCfgSettings?.Any() == true;

            if (!hasRegistrySettings && !hasCommandSettings && !hasPowerCfgSettings)
                return null;

            try
            {
                var tooltipData = new SettingTooltipData
                {
                    SettingId = setting.Id,
                    CommandSettings = setting.CommandSettings?.ToList() ?? new List<CommandSetting>(),
                    PowerCfgSettings = setting.PowerCfgSettings?.ToList() ?? new List<PowerCfgSetting>()
                };

                if (hasRegistrySettings)
                {
                    var registrySettings = setting.RegistrySettings.ToList();
                    var individualValues = new Dictionary<RegistrySetting, object?>();
                    var primaryRegistrySetting = registrySettings.First();
                    string primaryDisplayValue = "(not set)";

                    foreach (var registrySetting in registrySettings)
                    {
                        try
                        {
                            var currentValue = _registryService.GetValue(registrySetting.KeyPath, registrySetting.ValueName);
                            individualValues[registrySetting] = currentValue;

                            if (registrySetting == primaryRegistrySetting)
                            {
                                primaryDisplayValue = currentValue?.ToString() ?? "(not set)";
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

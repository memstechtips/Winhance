using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services
{
    public class ConfigurationCoordinatorService : IConfigurationCoordinatorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly ISettingApplicationService _settingApplicationService;

        private readonly IDomainService _startMenuService;
        private readonly IDomainService _taskbarService;
        private readonly IDomainService _explorerCustomizationService;
        private readonly IDomainService _windowsThemeService;
        private readonly IDomainService _gamingPerformanceService;
        private readonly IPowerService _powerService;
        private readonly IDomainService _privacyService;
        private readonly IDomainService _updateService;
        private readonly IDomainService _securityService;
        private readonly IDomainService _explorerOptimizationService;
        private readonly IDomainService _notificationService;
        private readonly IDomainService _soundService;

        public ConfigurationCoordinatorService(
            IServiceProvider serviceProvider,
            ILogService logService,
            ISettingApplicationService settingApplicationService,
            IDomainService startMenuService,
            IDomainService taskbarService,
            IDomainService explorerCustomizationService,
            IDomainService windowsThemeService,
            IDomainService gamingPerformanceService,
            IPowerService powerService,
            IDomainService privacyService,
            IDomainService updateService,
            IDomainService securityService,
            IDomainService explorerOptimizationService,
            IDomainService notificationService,
            IDomainService soundService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _settingApplicationService = settingApplicationService ?? throw new ArgumentNullException(nameof(settingApplicationService));
            _startMenuService = startMenuService ?? throw new ArgumentNullException(nameof(startMenuService));
            _taskbarService = taskbarService ?? throw new ArgumentNullException(nameof(taskbarService));
            _explorerCustomizationService = explorerCustomizationService ?? throw new ArgumentNullException(nameof(explorerCustomizationService));
            _windowsThemeService = windowsThemeService ?? throw new ArgumentNullException(nameof(windowsThemeService));
            _gamingPerformanceService = gamingPerformanceService ?? throw new ArgumentNullException(nameof(gamingPerformanceService));
            _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
            _privacyService = privacyService ?? throw new ArgumentNullException(nameof(privacyService));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _explorerOptimizationService = explorerOptimizationService ?? throw new ArgumentNullException(nameof(explorerOptimizationService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        }

        public async Task<UnifiedConfigurationFile> CreateUnifiedConfigurationAsync()
        {
            var unifiedConfig = new UnifiedConfigurationFile
            {
                Version = "2.0",
                CreatedAt = DateTime.UtcNow
            };

            var customizeSettings = new List<ISettingItem>();
            customizeSettings.AddRange(await _windowsThemeService.GetRawSettingsAsync());
            customizeSettings.AddRange(await _startMenuService.GetRawSettingsAsync());
            customizeSettings.AddRange(await _taskbarService.GetRawSettingsAsync());
            customizeSettings.AddRange(await _explorerCustomizationService.GetRawSettingsAsync());

            if (customizeSettings.Any())
            {
                unifiedConfig.Customize.IsIncluded = true;
                var customizeItems = await ConvertToConfigurationItemsAsync(customizeSettings);
                unifiedConfig.Customize.Items.AddRange(customizeItems);
            }

            var optimizeSettings = new List<ISettingItem>();
            optimizeSettings.AddRange(await _gamingPerformanceService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _powerService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _privacyService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _updateService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _securityService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _explorerOptimizationService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _notificationService.GetRawSettingsAsync());
            optimizeSettings.AddRange(await _soundService.GetRawSettingsAsync());

            if (optimizeSettings.Any())
            {
                unifiedConfig.Optimize.IsIncluded = true;
                var optimizeItems = await ConvertToConfigurationItemsAsync(optimizeSettings);
                unifiedConfig.Optimize.Items.AddRange(optimizeItems);
            }

            return unifiedConfig;
        }

        public async Task<bool> ApplyUnifiedConfigurationAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections)
        {
            var selectedSectionsList = selectedSections.ToList();
            bool success = true;

            if (selectedSectionsList.Contains("Customize") && config.Customize.IsIncluded)
            {
                var applied = await ApplyConfigurationItemsAsync(config.Customize.Items);
                success &= applied;
            }

            if (selectedSectionsList.Contains("Optimize") && config.Optimize.IsIncluded)
            {
                var applied = await ApplyConfigurationItemsAsync(config.Optimize.Items);
                success &= applied;
            }

            return success;
        }

        private async Task<List<ConfigurationItem>> ConvertToConfigurationItemsAsync(IEnumerable<ISettingItem> items)
        {
            var result = new List<ConfigurationItem>();

            foreach (var item in items)
            {
                var state = await _settingApplicationService.GetSettingStateAsync(item.Id);

                var configItem = new ConfigurationItem
                {
                    Name = item.Name,
                    IsSelected = state.Success ? state.IsEnabled : false,
                    InputType = item.InputType,
                };

                if (!string.IsNullOrEmpty(item.Id))
                    configItem.CustomProperties["Id"] = item.Id;

                if (!string.IsNullOrEmpty(item.GroupName))
                    configItem.CustomProperties["GroupName"] = item.GroupName;

                if (!string.IsNullOrEmpty(item.Description))
                    configItem.CustomProperties["Description"] = item.Description;

                if (state.Success && state.CurrentValue != null)
                    configItem.CustomProperties["CurrentValue"] = state.CurrentValue;

                configItem.EnsureSelectedValueIsSet();
                result.Add(configItem);
            }

            return result;
        }

        private async Task<bool> ApplyConfigurationItemsAsync(List<ConfigurationItem> items)
        {
            bool success = true;

            foreach (var item in items)
            {
                try
                {
                    if (item.CustomProperties.TryGetValue("Id", out var idObj) && idObj is string id)
                    {
                        object? value = null;
                        if (item.CustomProperties.TryGetValue("CurrentValue", out var currentValue))
                            value = currentValue;

                        await _settingApplicationService.ApplySettingAsync(id, item.IsSelected, value);
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error applying setting '{item.Name}': {ex.Message}");
                    success = false;
                }
            }

            return success;
        }
    }
}
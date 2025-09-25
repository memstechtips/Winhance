using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public class ConfigurationCoordinatorService : IConfigurationCoordinatorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly IUnifiedConfigurationService _unifiedConfigService;
        private readonly IDialogService _dialogService;
        private readonly IEventBus _eventBus;

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
            ISystemSettingsDiscoveryService discoveryService,
            IUnifiedConfigurationService unifiedConfigService,
            IDialogService dialogService,
            IEventBus eventBus,
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
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _unifiedConfigService = unifiedConfigService ?? throw new ArgumentNullException(nameof(unifiedConfigService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
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
            customizeSettings.AddRange(await _windowsThemeService.GetSettingsAsync());
            customizeSettings.AddRange(await _startMenuService.GetSettingsAsync());
            customizeSettings.AddRange(await _taskbarService.GetSettingsAsync());
            customizeSettings.AddRange(await _explorerCustomizationService.GetSettingsAsync());

            if (customizeSettings.Any())
            {
                unifiedConfig.Customize.IsIncluded = true;
                var customizeItems = await ConvertToConfigurationItemsAsync(customizeSettings);
                unifiedConfig.Customize.Items.AddRange(customizeItems);
            }

            var optimizeSettings = new List<ISettingItem>();
            optimizeSettings.AddRange(await _gamingPerformanceService.GetSettingsAsync());
            optimizeSettings.AddRange(await _powerService.GetSettingsAsync());
            optimizeSettings.AddRange(await _privacyService.GetSettingsAsync());
            optimizeSettings.AddRange(await _updateService.GetSettingsAsync());
            optimizeSettings.AddRange(await _securityService.GetSettingsAsync());
            optimizeSettings.AddRange(await _explorerOptimizationService.GetSettingsAsync());
            optimizeSettings.AddRange(await _notificationService.GetSettingsAsync());
            optimizeSettings.AddRange(await _soundService.GetSettingsAsync());

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
                if (item is not SettingDefinition settingDefinition)
                    continue;

                var results = await _discoveryService.GetSettingStatesAsync(new[] { settingDefinition });
                var state = results.TryGetValue(item.Id, out var settingState) ? settingState : new SettingStateResult();

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

        public async Task SaveUnifiedConfigAsync()
        {
            try
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = "Using UnifiedConfigurationService to save unified configuration",
                    Level = LogLevel.Info,
                });

                var unifiedConfig = await _unifiedConfigService.CreateUnifiedConfigurationAsync();
                var configService = GetConfigurationService();

                if (configService == null)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "ConfigurationService not available",
                        Level = LogLevel.Error,
                    });
                    return;
                }

                bool saveResult = await _unifiedConfigService.SaveUnifiedConfigurationAsync(unifiedConfig);

                if (saveResult)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "Unified configuration saved successfully",
                        Level = LogLevel.Info,
                    });

                    var sections = new List<string>();
                    if (unifiedConfig.WindowsApps.Items.Any()) sections.Add("Windows Apps");
                    if (unifiedConfig.ExternalApps.Items.Any()) sections.Add("External Apps");
                    if (unifiedConfig.Customize.Items.Any()) sections.Add("Customizations");
                    if (unifiedConfig.Optimize.Items.Any()) sections.Add("Optimizations");

                    CustomDialog.ShowInformation(
                        "Configuration Saved",
                        "Configuration saved successfully.",
                        sections,
                        "You can now import this configuration on another system."
                    );
                }
                else
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "Save unified configuration canceled by user",
                        Level = LogLevel.Info,
                    });
                }
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = $"Error saving unified configuration: {ex.Message}",
                    Level = LogLevel.Error,
                    Exception = ex,
                });
            }
        }

        public async Task ImportUnifiedConfigAsync()
        {
            try
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = "Starting unified configuration import process",
                    Level = LogLevel.Info,
                });

                var configService = GetConfigurationService();
                if (configService == null)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "ConfigurationService not available",
                        Level = LogLevel.Error,
                    });
                    return;
                }

                var selectedOption = await _dialogService.ShowConfigImportOptionsDialogAsync();
                if (selectedOption == null)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "User canceled config import options dialog",
                        Level = LogLevel.Info,
                    });
                    return;
                }

                UnifiedConfigurationFile unifiedConfig = selectedOption switch
                {
                    ImportOption.ImportOwn => await _unifiedConfigService.LoadUnifiedConfigurationAsync(),
                    ImportOption.ImportRecommended => await configService.LoadRecommendedConfigurationAsync(),
                    _ => null
                };

                if (unifiedConfig == null)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "Import unified configuration canceled by user",
                        Level = LogLevel.Info,
                    });
                    return;
                }

                var sectionInfo = CreateSectionInfo(unifiedConfig);
                var result = await _dialogService.ShowUnifiedConfigurationImportDialogAsync(
                    "Select Configuration Sections",
                    "Select which sections you want to import from the unified configuration.",
                    sectionInfo
                );

                if (result == null)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "User canceled unified configuration import",
                        Level = LogLevel.Info,
                    });
                    return;
                }

                var selectedSections = result.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                if (!selectedSections.Any())
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = "No sections selected for import",
                        Level = LogLevel.Info,
                    });
                    _dialogService.ShowMessage(
                        "Please select at least one section to import from the unified configuration.",
                        "No sections selected"
                    );
                    return;
                }

                await _unifiedConfigService.ApplyUnifiedConfigurationAsync(unifiedConfig, selectedSections);

                _eventBus.Publish(new LogEvent
                {
                    Message = "Unified configuration imported successfully",
                    Level = LogLevel.Info,
                });

                ShowImportSuccessMessage(selectedSections);
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = $"Error importing unified configuration: {ex.Message}",
                    Level = LogLevel.Error,
                    Exception = ex,
                });

                _dialogService?.ShowMessage(
                    $"An error occurred while importing the configuration: {ex.Message}",
                    "Import Error"
                );
            }
        }

        private IConfigurationService GetConfigurationService()
        {
            if (Application.Current is not App appInstance) return null;

            try
            {
                var hostField = appInstance.GetType().GetField("_host", BindingFlags.NonPublic | BindingFlags.Instance);
                var host = hostField?.GetValue(appInstance);
                if (host == null) return null;

                var servicesProperty = host.GetType().GetProperty("Services");
                var services = servicesProperty?.GetValue(host);
                if (services == null) return null;

                var getServiceMethod = services.GetType().GetMethod("GetService", new[] { typeof(Type) });
                return getServiceMethod?.Invoke(services, new object[] { typeof(IConfigurationService) }) as IConfigurationService;
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = $"Error accessing ConfigurationService: {ex.Message}",
                    Level = LogLevel.Error,
                    Exception = ex,
                });
                return null;
            }
        }

        private static Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> CreateSectionInfo(UnifiedConfigurationFile unifiedConfig)
        {
            return new Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)>
            {
                { "Software & Apps", (true, unifiedConfig.WindowsApps.Items.Count > 0 || unifiedConfig.ExternalApps.Items.Count > 0, unifiedConfig.WindowsApps.Items.Count + unifiedConfig.ExternalApps.Items.Count) },
                { "WindowsApps", (true, unifiedConfig.WindowsApps.Items.Count > 0, unifiedConfig.WindowsApps.Items.Count) },
                { "ExternalApps", (true, unifiedConfig.ExternalApps.Items.Count > 0, unifiedConfig.ExternalApps.Items.Count) },
                { "Optimize", (true, unifiedConfig.Optimize.Items.Count > 0, unifiedConfig.Optimize.Items.Count) },
                { "Optimize.GamingAndPerformance", (true, true, 0) },
                { "Optimize.PowerSettings", (true, true, 0) },
                { "Optimize.WindowsSecuritySettings", (true, true, 0) },
                { "Optimize.PrivacySettings", (true, true, 0) },
                { "Optimize.WindowsUpdates", (true, true, 0) },
                { "Optimize.Explorer", (true, true, 0) },
                { "Optimize.Notifications", (true, true, 0) },
                { "Optimize.Sound", (true, true, 0) },
                { "Customize", (true, unifiedConfig.Customize.Items.Count > 0, unifiedConfig.Customize.Items.Count) },
                { "Customize.WindowsTheme", (true, true, 0) },
                { "Customize.Taskbar", (true, true, 0) },
                { "Customize.StartMenu", (true, true, 0) },
                { "Customize.Explorer", (true, true, 0) },
            };
        }

        private static void ShowImportSuccessMessage(List<string> selectedSections)
        {
            var importedSections = selectedSections.Select(section => section switch
            {
                "WindowsApps" => "Windows Apps",
                "ExternalApps" => "External Apps",
                "Customize" => "Customizations",
                "Optimize" => "Optimizations",
                _ => section
            }).ToList();

            CustomDialog.ShowInformation(
                "Configuration Imported",
                "The unified configuration has been imported successfully.",
                importedSections,
                "The selected settings have been applied to your system."
            );
        }
    }
}
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class WindowsThemeCustomizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      IComboBoxResolver comboBoxResolver,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.WindowsTheme;
        public override string DisplayName => "Windows Theme";

        public override async Task<bool> HandleDomainContextSettingAsync(SettingDefinition setting, object? value, bool additionalContext = false)
        {
            try
            {
                logService.Log(LogLevel.Info, $"[WindowsThemeCustomizationsViewModel] Handling domain context setting '{setting.Id}'");

                if (setting.InputType == InputType.Selection)
                {
                    var themeService = domainServiceRouter.GetDomainService(ModuleId);
                    if (themeService == null)
                    {
                        logService.Log(LogLevel.Error, $"WindowsThemeService not available for '{setting.Id}'");
                        return false;
                    }

                    Dictionary<string, int?>? registryValues = null;
                    if (value is int index && setting.RegistrySettings?.Count > 0)
                    {
                        var rawValues = comboBoxResolver.ResolveIndexToRawValues(setting, index);
                        registryValues = rawValues.ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value != null ? Convert.ToInt32(kv.Value) : (int?)null
                        );
                        logService.Log(LogLevel.Info, $"[WindowsThemeCustomizationsViewModel] Resolved combo box index {index} to registry values: {string.Join(", ", registryValues.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    }

                    var context = new SettingOperationContext 
                    { 
                        SettingId = setting.Id, 
                        Enable = true, 
                        Value = value,
                        ApplyWallpaper = additionalContext,
                        RegistryValues = registryValues
                    };

                    dynamic service = themeService;
                    await service.ApplySettingWithContextAsync(setting.Id, true, value, context);
                    
                    logService.Log(LogLevel.Info, $"[WindowsThemeCustomizationsViewModel] Successfully applied theme setting");
                    return true;
                }

                logService.Log(LogLevel.Warning, $"[WindowsThemeCustomizationsViewModel] Unknown domain context setting type: '{setting.Id}'");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsThemeCustomizationsViewModel] Error handling setting '{setting.Id}': {ex.Message}");
                return false;
            }
        }
    }
}

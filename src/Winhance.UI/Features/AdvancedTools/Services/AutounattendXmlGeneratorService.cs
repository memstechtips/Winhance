using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.AdvancedTools.Services;

public class AutounattendXmlGeneratorService : IAutounattendXmlGeneratorService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ILogService _logService;
    private readonly AutounattendScriptBuilder _scriptBuilder;

    public AutounattendXmlGeneratorService(
        IServiceProvider serviceProvider,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISystemSettingsDiscoveryService discoveryService,
        ILogService logService,
        AutounattendScriptBuilder scriptBuilder)
    {
        _serviceProvider = serviceProvider;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _discoveryService = discoveryService;
        _logService = logService;
        _scriptBuilder = scriptBuilder;
    }

    public async Task<string> GenerateFromCurrentSelectionsAsync(string outputPath)
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting autounattend.xml generation");

            var config = await CreateConfigurationFromSystemAsync();

            var allSettings = _compatibleSettingsRegistry.GetAllFilteredSettings();

            var scriptContent = await _scriptBuilder.BuildWinhancementsScriptAsync(config, allSettings);

            var xmlTemplate = LoadEmbeddedTemplate();

            var finalXml = InjectScriptIntoTemplate(xmlTemplate, scriptContent);

            // Write without BOM (Byte Order Mark) - Windows Setup requires UTF-8 without BOM
            var utf8WithoutBom = new UTF8Encoding(false);
            await File.WriteAllTextAsync(outputPath, finalXml, utf8WithoutBom);

            _logService.Log(LogLevel.Info, $"Autounattend.xml generated successfully: {outputPath}");
            return outputPath;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error generating autounattend.xml: {ex.Message}");
            throw;
        }
    }

    private async Task<UnifiedConfigurationFile> CreateConfigurationFromSystemAsync()
    {
        var config = new UnifiedConfigurationFile
        {
            Version = "2.0",
            CreatedAt = DateTime.UtcNow
        };

        await PopulateFeatureBasedSections(config);
        await PopulateAppsSections(config);

        return config;
    }

    private async Task PopulateFeatureBasedSections(UnifiedConfigurationFile config)
    {
        var allSettingsByFeature = _compatibleSettingsRegistry.GetAllFilteredSettings();

        int totalOptimizeSettings = 0;
        int totalCustomizeSettings = 0;

        foreach (var kvp in allSettingsByFeature)
        {
            var featureId = kvp.Key;
            var settings = kvp.Value.ToList();

            if (!settings.Any())
                continue;

            var isOptimize = FeatureDefinitions.OptimizeFeatures.Contains(featureId);
            var isCustomize = FeatureDefinitions.CustomizeFeatures.Contains(featureId);

            if (!isOptimize && !isCustomize)
            {
                _logService.Log(LogLevel.Warning, $"Feature {featureId} is neither Optimize nor Customize, skipping");
                continue;
            }

            var states = await _discoveryService.GetSettingStatesAsync(settings);

            var items = settings.Select(setting =>
            {
                var state = states.GetValueOrDefault(setting.Id);

                var item = new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Name,
                    InputType = setting.InputType
                };

                if (setting.InputType == InputType.Toggle)
                {
                    item.IsSelected = state?.IsEnabled ?? false;
                }
                else if (setting.InputType == InputType.Selection)
                {
                    var (selectedIndex, customStateValues, powerPlanGuid, powerPlanName) = GetSelectionStateFromState(setting, state);

                    if (setting.Id == "power-plan-selection")
                    {
                        item.PowerPlanGuid = powerPlanGuid;
                        item.PowerPlanName = powerPlanName;
                    }
                    else
                    {
                        item.SelectedIndex = selectedIndex;
                    }
                }

                if (setting.InputType == InputType.Selection &&
                    setting.PowerCfgSettings?.Any() == true &&
                    setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                    state?.CurrentValue is Dictionary<string, object> powerDict)
                {
                    item.PowerSettings = powerDict;
                }

                if (state?.RawValues != null && state.RawValues.Count > 0)
                {
                    item.CustomStateValues = state.RawValues.ToDictionary(
                        k => k.Key,
                        v => (object)v.Value);
                }

                return item;
            }).ToList();

            var section = new ConfigSection
            {
                IsIncluded = true,
                Items = items
            };

            if (isOptimize)
            {
                config.Optimize.Features[featureId] = section;
                config.Optimize.IsIncluded = true;
                totalOptimizeSettings += items.Count;
                _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Optimize)");
            }
            else
            {
                config.Customize.Features[featureId] = section;
                config.Customize.IsIncluded = true;
                totalCustomizeSettings += items.Count;
                _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Customize)");
            }
        }

        _logService.Log(LogLevel.Info, $"Total exported: {totalOptimizeSettings} Optimize settings, {totalCustomizeSettings} Customize settings");
    }

    private async Task PopulateAppsSections(UnifiedConfigurationFile config)
    {
        var windowsAppsVM = _serviceProvider.GetService<WindowsAppsViewModel>();
        if (windowsAppsVM != null)
        {
            if (!windowsAppsVM.IsInitialized)
                await windowsAppsVM.LoadItemsAsync();

            config.WindowsApps.IsIncluded = true;
            config.WindowsApps.Items = windowsAppsVM.Items
                .Where(item => item.IsSelected)
                .Select(item =>
                {
                    var configItem = new ConfigurationItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        IsSelected = true,
                        InputType = InputType.Toggle
                    };

                    if (!string.IsNullOrEmpty(item.Definition.AppxPackageName))
                    {
                        configItem.AppxPackageName = item.Definition.AppxPackageName;
                        if (item.Definition.SubPackages?.Length > 0)
                            configItem.SubPackages = item.Definition.SubPackages;
                    }
                    else if (!string.IsNullOrEmpty(item.Definition.CapabilityName))
                        configItem.CapabilityName = item.Definition.CapabilityName;
                    else if (!string.IsNullOrEmpty(item.Definition.OptionalFeatureName))
                        configItem.OptionalFeatureName = item.Definition.OptionalFeatureName;

                    return configItem;
                }).ToList();

            _logService.Log(LogLevel.Info, $"Exported {config.WindowsApps.Items.Count} checked Windows Apps");
        }
    }

    private (int? selectedIndex, Dictionary<string, object> customStateValues, string powerPlanGuid, string powerPlanName)
        GetSelectionStateFromState(SettingDefinition setting, SettingStateResult state)
    {
        if (setting.InputType != InputType.Selection)
            return (null, null, null, null);

        if (state?.CurrentValue is not int index)
            return (0, null, null, null);

        if (setting.Id == "power-plan-selection" && state.RawValues != null)
        {
            var guid = state.RawValues.TryGetValue("ActivePowerPlanGuid", out var g) ? g?.ToString() : null;
            var name = state.RawValues.TryGetValue("ActivePowerPlan", out var n) ? n?.ToString() : null;

            _logService.Log(LogLevel.Info, $"[AutounattendXmlGeneratorService] Exporting power plan: {name} ({guid})");
            return (index, null, guid, name);
        }

        if (index == ComboBoxResolver.CUSTOM_STATE_INDEX)
        {
            var customValues = new Dictionary<string, object>();

            if (state.RawValues != null)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var key = registrySetting.ValueName ?? "KeyExists";
                    if (state.RawValues.TryGetValue(key, out var value) && value != null)
                    {
                        customValues[key] = value;
                    }
                }
            }

            return (null, customValues.Count > 0 ? customValues : null, null, null);
        }

        return (index, null, null, null);
    }

    private string LoadEmbeddedTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Winhance.UI.Resources.AdvancedTools.autounattend-template.xml";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded template not found: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string InjectScriptIntoTemplate(string template, string scriptContent)
    {
        const string placeholder = "<!--SCRIPT_PLACEHOLDER-->";
        const string replacement = "<![CDATA[{0}]]>";

        if (!template.Contains(placeholder))
            throw new InvalidOperationException("Script placeholder not found in template");

        return template.Replace(placeholder, string.Format(replacement, scriptContent));
    }
}

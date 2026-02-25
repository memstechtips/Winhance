using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;

namespace Winhance.UI.Features.AdvancedTools.Services;

public class AutounattendXmlGeneratorService : IAutounattendXmlGeneratorService
{
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ILogService _logService;
    private readonly AutounattendScriptBuilder _scriptBuilder;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly ISelectedAppsProvider _selectedAppsProvider;

    public AutounattendXmlGeneratorService(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISystemSettingsDiscoveryService discoveryService,
        ILogService logService,
        AutounattendScriptBuilder scriptBuilder,
        IPowerShellRunner powerShellRunner,
        ISelectedAppsProvider selectedAppsProvider)
    {
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _discoveryService = discoveryService;
        _logService = logService;
        _scriptBuilder = scriptBuilder;
        _powerShellRunner = powerShellRunner;
        _selectedAppsProvider = selectedAppsProvider;
    }

    public async Task<string> GenerateFromCurrentSelectionsAsync(string outputPath,
        IReadOnlyList<ConfigurationItem>? selectedWindowsApps = null)
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting autounattend.xml generation");

            var apps = selectedWindowsApps
                ?? await _selectedAppsProvider.GetSelectedWindowsAppsAsync();

            var config = await CreateConfigurationFromSystemAsync(apps);

            var allSettings = _compatibleSettingsRegistry.GetAllFilteredSettings();

            var scriptContent = await _scriptBuilder.BuildWinhancementsScriptAsync(config, allSettings);

            var xmlTemplate = LoadEmbeddedTemplate();

            var finalXml = InjectScriptIntoTemplate(xmlTemplate, scriptContent);

            // Validate the final XML is well-formed
            try
            {
                await _powerShellRunner.ValidateXmlSyntaxAsync(finalXml);
                _logService.Log(LogLevel.Info, "autounattend.xml passed XML well-formedness validation");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"autounattend.xml failed XML well-formedness validation: {ex.Message}");
                throw;
            }

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

    private async Task<UnifiedConfigurationFile> CreateConfigurationFromSystemAsync(
        IReadOnlyList<ConfigurationItem>? selectedWindowsApps)
    {
        var config = new UnifiedConfigurationFile
        {
            Version = "2.0",
            CreatedAt = DateTime.UtcNow
        };

        await PopulateFeatureBasedSections(config);
        PopulateAppsSections(config, selectedWindowsApps);

        return config;
    }

    private async Task PopulateFeatureBasedSections(UnifiedConfigurationFile config)
    {
        var allSettingsByFeature = _compatibleSettingsRegistry.GetAllFilteredSettings();

        int totalOptimizeSettings = 0;
        int totalCustomizeSettings = 0;
        var optimizeFeatures = new Dictionary<string, ConfigSection>();
        var customizeFeatures = new Dictionary<string, ConfigSection>();

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
                    item.CustomStateValues = state.RawValues
                        .Where(v => v.Value != null)
                        .ToDictionary(k => k.Key, v => v.Value!);
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
                optimizeFeatures[featureId] = section;
                config.Optimize.IsIncluded = true;
                totalOptimizeSettings += items.Count;
                _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Optimize)");
            }
            else
            {
                customizeFeatures[featureId] = section;
                config.Customize.IsIncluded = true;
                totalCustomizeSettings += items.Count;
                _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Customize)");
            }
        }

        config.Optimize.Features = optimizeFeatures;
        config.Customize.Features = customizeFeatures;
        _logService.Log(LogLevel.Info, $"Total exported: {totalOptimizeSettings} Optimize settings, {totalCustomizeSettings} Customize settings");
    }

    private void PopulateAppsSections(UnifiedConfigurationFile config,
        IReadOnlyList<ConfigurationItem>? selectedWindowsApps)
    {
        if (selectedWindowsApps != null && selectedWindowsApps.Count > 0)
        {
            config.WindowsApps.IsIncluded = true;
            config.WindowsApps.Items = selectedWindowsApps.ToList();
            _logService.Log(LogLevel.Info, $"Exported {config.WindowsApps.Items.Count} checked Windows Apps");
        }
    }

    private (int? selectedIndex, Dictionary<string, object>? customStateValues, string? powerPlanGuid, string? powerPlanName)
        GetSelectionStateFromState(SettingDefinition setting, SettingStateResult? state)
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

        if (index == ComboBoxConstants.CustomStateIndex)
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

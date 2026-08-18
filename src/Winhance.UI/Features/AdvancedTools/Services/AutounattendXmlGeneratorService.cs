using System.Reflection;
using System.Text;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.Services;

public class AutounattendXmlGeneratorService : IAutounattendXmlGeneratorService
{
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly IWindowsVersionFilterService _windowsVersionFilter;
    private readonly ICatalogSettingStateProvider _settingStateProvider;
    private readonly ILogService _logService;
    private readonly AutounattendScriptBuilder _scriptBuilder;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly ISelectedAppsProvider _selectedAppsProvider;

    public AutounattendXmlGeneratorService(
        ICatalogSettingsRegistry catalogSettingsRegistry,
        IWindowsVersionFilterService windowsVersionFilter,
        ICatalogSettingStateProvider settingStateProvider,
        ILogService logService,
        AutounattendScriptBuilder scriptBuilder,
        IPowerShellRunner powerShellRunner,
        ISelectedAppsProvider selectedAppsProvider)
    {
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _windowsVersionFilter = windowsVersionFilter;
        _settingStateProvider = settingStateProvider;
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

            // Ensure the catalog registry is initialized (idempotent) - self-heals a degraded
            // startup on every generator entry point.
            await _catalogSettingsRegistry.InitializeAsync();

            var apps = selectedWindowsApps
                ?? await _selectedAppsProvider.GetSelectedWindowsAppsAsync();

            var config = await CreateConfigurationFromSystemAsync(apps);

            return await RenderConfigToXmlAsync(config, outputPath);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error generating autounattend.xml: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GenerateFromConfigAsync(UnifiedConfigurationFile config, string outputPath)
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting autounattend.xml generation from Builder config");
            await _catalogSettingsRegistry.InitializeAsync();
            return await RenderConfigToXmlAsync(config, outputPath);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error generating autounattend.xml from config: {ex.Message}");
            throw;
        }
    }

    private async Task<string> RenderConfigToXmlAsync(UnifiedConfigurationFile config, string outputPath)
    {
        var allSettings = _catalogSettingsRegistry.GetAll(includeOtherOsVersions: !_windowsVersionFilter.IsFilterEnabled);

        var scriptContent = await _scriptBuilder.BuildWinhancementsScriptAsync(config, allSettings);

        var xmlTemplate = LoadEmbeddedTemplate();

        var finalXml = InjectScriptIntoTemplate(xmlTemplate, scriptContent);

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
        var allSettingsByFeature = _catalogSettingsRegistry.GetAll(includeOtherOsVersions: !_windowsVersionFilter.IsFilterEnabled);

        int totalOptimizeSettings = 0;
        int totalCustomizeSettings = 0;
        var optimizeFeatures = new Dictionary<string, ConfigSection>();
        var customizeFeatures = new Dictionary<string, ConfigSection>();

        foreach (var kvp in allSettingsByFeature)
        {
            var featureId = kvp.Key;
            var settings = kvp.Value.ToList();

            if (settings.Count == 0)
                continue;

            var isOptimize = FeatureDefinitions.OptimizeFeatures.Contains(featureId);
            var isCustomize = FeatureDefinitions.CustomizeFeatures.Contains(featureId);

            if (!isOptimize && !isCustomize)
            {
                _logService.Log(LogLevel.Warning, $"Feature {featureId} is neither Optimize nor Customize, skipping");
                continue;
            }

            var states = await _settingStateProvider.GetStatesAsync(settings);

            var items = settings.Select(setting =>
            {
                var state = states.GetValueOrDefault(setting.Id);

                var item = new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Display.Name,
                    InputType = ControlToInputType(setting.Control)
                };

                bool isToggle = setting.Control == ControlKind.Toggle;
                bool isSelection = setting.Control is ControlKind.Selection or ControlKind.PowerPlan;
                bool isPowerCfgSeparate = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Mode == PowerModeSupport.Separate;

                if (isToggle)
                {
                    item.IsSelected = state?.IsEnabled ?? false;
                }
                else if (isSelection)
                {
                    var (selectedIndex, customStateValues, powerPlanGuid, powerPlanName) = GetSelectionStateFromState(setting, state);

                    if (setting.Id == SettingIds.PowerPlanSelection)
                    {
                        item.PowerPlanGuid = powerPlanGuid;
                        item.PowerPlanName = powerPlanName;
                    }
                    else
                    {
                        // A Separate-mode powercfg Selection exports AC and DC indices distinctly (mirror
                        // ConfigExportService's Selection AC/DC branch). AcValue/DcValue are the typed fields
                        // the catalog detection overlay populates; the generated unattend sets AC and DC
                        // separately on install for these settings.
                        bool hasAcDcPowerSettings = false;

                        if (isPowerCfgSeparate && state != null)
                        {
                            object? acValue = state.AcValue;
                            object? dcValue = state.DcValue;

                            if (acValue != null || dcValue != null)
                            {
                                var acIndex = ResolveValueToIndex(setting, acValue);
                                var dcIndex = ResolveValueToIndex(setting, dcValue);

                                item.PowerSettings = new Dictionary<string, object>
                                {
                                    ["ACIndex"] = acIndex,
                                    ["DCIndex"] = dcIndex
                                };
                                hasAcDcPowerSettings = true;
                            }
                        }

                        if (!hasAcDcPowerSettings)
                        {
                            item.SelectedIndex = selectedIndex;
                        }
                    }
                }

                if (isSelection &&
                    item.SelectedIndex == null &&
                    item.PowerSettings == null &&
                    setting.Id != SettingIds.PowerPlanSelection &&
                    state != null)
                {
                    var custom = CustomStateValueReconstructor.Build(setting, state)
                        .Where(v => v.Value != null)
                        .ToDictionary(k => k.Key, v => v.Value!);
                    if (custom.Count > 0)
                        item.CustomStateValues = custom;
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
        GetSelectionStateFromState(Setting setting, SettingStateResult? state)
    {
        bool isSelection = setting.Control is ControlKind.Selection or ControlKind.PowerPlan;
        if (!isSelection)
            return (null, null, null, null);

        if (state?.CurrentValue is not int index)
            return (0, null, null, null);

        if (setting.Id == SettingIds.PowerPlanSelection)
        {
            // Source the active-plan GUID from DynamicSelection (the active scheme GUID, lowercased).
            // powercfg GUIDs are case-insensitive. The display NAME reads the typed DynamicSelectionName
            // (the active plan's raw OS name).
            var guid = state.DynamicSelection;
            var name = state.DynamicSelectionName;

            _logService.Log(LogLevel.Info, $"[AutounattendXmlGeneratorService] Exporting power plan: {name} ({guid})");
            return (index, null, guid, name);
        }

        if (index == ComboBoxConstants.CustomStateIndex)
        {
            var customValues = new Dictionary<string, object>();

            // (This GetSelectionStateFromState custom-state result is discarded by the autounattend.)
            if (state.Readings != null)
            {
                var regKeys = setting.Targets.OfType<RegTarget>().Select(rt => rt.ValueName ?? "KeyExists");
                foreach (var key in regKeys)
                {
                    if (state.Readings.TryGetValue(key, out var value) && value != null)
                    {
                        customValues[key] = value;
                    }
                }
            }

            return (null, customValues.Count > 0 ? customValues : null, null, null);
        }

        return (index, null, null, null);
    }

    // Index resolver for a Separate-mode powercfg Selection's AC/DC values - verbatim mirror of
    // ConfigExportService.ResolveValueToIndex so both exporters resolve a raw powercfg value to its option
    // index identically (via the catalog States' Set["Power"] payload).
    private static int ResolveValueToIndex(Setting setting, object? value)
    {
        if (value == null) return 0;

        var intValue = Convert.ToInt32(value);

        for (int i = 0; i < setting.States.Count; i++)
        {
            if (setting.States[i].Set.TryGetValue("Power", out var sv) &&
                sv.WritePayload != null && Convert.ToInt32(sv.WritePayload) == intValue)
            {
                return i;
            }
        }

        return 0;
    }

    // Twin of ConfigExportService.ControlToInputType: the persisted InputType field is LOAD-BEARING
    // (ConfigMigrationService's import gates read it, and it seeds the ViewModel InputType on import).
    private static InputType ControlToInputType(ControlKind control) => control switch
    {
        ControlKind.Selection or ControlKind.PowerPlan => InputType.Selection,
        ControlKind.Slider => InputType.NumericRange,
        ControlKind.Action => InputType.Action,
        _ => InputType.Toggle,
    };

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

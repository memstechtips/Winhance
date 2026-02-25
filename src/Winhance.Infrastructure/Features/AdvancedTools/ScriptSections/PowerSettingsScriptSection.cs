using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

/// <summary>
/// Handles power plan creation, power settings extraction and application in the generated script.
/// </summary>
internal class PowerSettingsScriptSection
{
    private readonly IPowerSettingsQueryService _powerSettingsQueryService;
    private readonly IHardwareDetectionService _hardwareDetectionService;
    private readonly ILogService _logService;

    private class PowerSettingData
    {
        public string SubgroupGuid { get; set; } = string.Empty;
        public string SettingGuid { get; set; } = string.Empty;
        public int AcValue { get; set; }
        public int DcValue { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public PowerSettingsScriptSection(
        IPowerSettingsQueryService powerSettingsQueryService,
        IHardwareDetectionService hardwareDetectionService,
        ILogService logService)
    {
        _powerSettingsQueryService = powerSettingsQueryService;
        _hardwareDetectionService = hardwareDetectionService;
        _logService = logService;
    }

    public ConfigurationItem? FindPowerPlanSetting(
        UnifiedConfigurationFile config,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        if (!config.Optimize.Features.TryGetValue(FeatureIds.Power, out var powerSection))
            return null;

        return powerSection.Items.FirstOrDefault(item =>
            item.Id == "power-plan-selection" && !string.IsNullOrEmpty(item.PowerPlanGuid));
    }

    public async Task<bool> AppendPowerSettingsSectionAsync(
        StringBuilder sb,
        UnifiedConfigurationFile config,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings,
        string indent)
    {
        var powerPlanSetting = FindPowerPlanSetting(config, allSettings);
        var activePowerPlan = await _powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
        var powerSettings = await ExtractPowerSettingsAsync(activePowerPlan.Guid, allSettings).ConfigureAwait(false);

        if (powerPlanSetting == null && !powerSettings.Any())
            return false;

        AppendPowerSettingsSection(sb, powerPlanSetting, powerSettings, indent);
        return true;
    }

    private void AppendPowerSettingsSection(
        StringBuilder sb,
        ConfigurationItem? powerPlanSetting,
        List<PowerSettingData> powerSettings,
        string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# POWER PLAN & POWERCFG SETTINGS");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();

        if (powerPlanSetting != null)
        {
            AppendPowerPlanCreation(sb, powerPlanSetting, indent);
        }

        if (powerSettings.Any())
        {
            AppendPowerSettingsApplication(sb, powerSettings, powerPlanSetting?.PowerPlanGuid, indent);
        }
    }

    private void AppendPowerPlanCreation(StringBuilder sb, ConfigurationItem powerPlanSetting, string indent)
    {
        var planGuid = powerPlanSetting.PowerPlanGuid;
        var planName = powerPlanSetting.PowerPlanName;

        sb.AppendLine($"{indent}Write-Log \"Setting up power plan: {planName}...\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}$customPlanGuid = \"{planGuid}\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}$existingPlan = powercfg /query $customPlanGuid 2>&1");
        sb.AppendLine($"{indent}$planExists = $LASTEXITCODE -eq 0");
        sb.AppendLine();
        sb.AppendLine($"{indent}if ($planExists) {{");
        sb.AppendLine($"{indent}    Write-Log \"Power plan already exists, using existing plan\" \"INFO\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    Write-Log \"Creating new power plan...\" \"INFO\"");
        sb.AppendLine($"{indent}    $planCreated = $false");
        sb.AppendLine();
        sb.AppendLine($"{indent}    $sourceSchemes = @(");
        sb.AppendLine($"{indent}        @{{ Name = \"Ultimate Performance\"; Guid = \"e9a42b02-d5df-448d-aa00-03f14749eb61\" }},");
        sb.AppendLine($"{indent}        @{{ Name = \"High Performance\"; Guid = \"8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c\" }},");
        sb.AppendLine($"{indent}        @{{ Name = \"Balanced\"; Guid = \"381b4222-f694-41f0-9685-ff5bb260df2e\" }}");
        sb.AppendLine($"{indent}    )");
        sb.AppendLine();
        sb.AppendLine($"{indent}    foreach ($scheme in $sourceSchemes) {{");
        sb.AppendLine($"{indent}        Write-Log \"Attempting to duplicate from $($scheme.Name)...\" \"INFO\"");
        sb.AppendLine($"{indent}        $result = powercfg /duplicatescheme $($scheme.Guid) $customPlanGuid 2>&1");
        sb.AppendLine($"{indent}        if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}            Write-Log \"Successfully created from $($scheme.Name)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            powercfg /changename $customPlanGuid \"{planName}\" | Out-Null");
        sb.AppendLine($"{indent}            $planCreated = $true");
        sb.AppendLine($"{indent}            break");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    if (-not $planCreated) {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to create power plan\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendPowerSettingsApplication(StringBuilder sb, List<PowerSettingData> powerSettings, string? powerPlanGuid, string indent)
    {
        sb.AppendLine($"{indent}Write-Log \"Enabling hidden power settings...\" \"INFO\"");
        sb.AppendLine($"{indent}$PowerSettingsBasePath = \"HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerSettings\"");
        sb.AppendLine($"{indent}$hiddenSettings = @(");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"2a737441-1930-4402-8d77-b2bebba308a3\"; Setting = \"0853a681-27c8-4100-a2fd-82013e970683\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"2a737441-1930-4402-8d77-b2bebba308a3\"; Setting = \"d4e98f31-5ffe-4ce1-be31-1b38b384c009\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"4f971e89-eebd-4455-a8de-9e59040e7347\"; Setting = \"7648efa3-dd9c-4e3e-b566-50f929386280\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"4f971e89-eebd-4455-a8de-9e59040e7347\"; Setting = \"96996bc0-ad50-47ec-923b-6f41874dd9eb\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"4f971e89-eebd-4455-a8de-9e59040e7347\"; Setting = \"5ca83367-6e45-459f-a27b-476b1d01c936\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"94d3a615-a899-4ac5-ae2b-e4d8f634367f\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"be337238-0d82-4146-a960-4f3749d470c7\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"465e1f50-b610-473a-ab58-00d1077dc418\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"40fbefc7-2e9d-4d25-a185-0cfd8574bac6\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"0cc5b647-c1df-4637-891a-dec35c318583\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"ea062031-0e34-4ff1-9b6d-eb1059334028\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"36687f9e-e3a5-4dbf-b1dc-15eb381c6863\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"06cadf0e-64ed-448a-8927-ce7bf90eb35d\" }},");
        sb.AppendLine($"{indent}    @{{ Subgroup = \"54533251-82be-4824-96c1-47b60b740d00\"; Setting = \"12a0ab44-fe28-4fa9-b3bd-4b64f44960a6\" }}");
        sb.AppendLine($"{indent})");
        sb.AppendLine();
        sb.AppendLine($"{indent}$enabledCount = 0");
        sb.AppendLine($"{indent}foreach ($item in $hiddenSettings) {{");
        sb.AppendLine($"{indent}    $regPath = Join-Path $PowerSettingsBasePath \"$($item.Subgroup)\\$($item.Setting)\"");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        if (Test-Path $regPath) {{");
        sb.AppendLine($"{indent}            Set-ItemProperty -Path $regPath -Name \"Attributes\" -Value 0 -Type DWord -ErrorAction Stop");
        sb.AppendLine($"{indent}            $enabledCount++");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Enabled $enabledCount hidden power settings\" \"SUCCESS\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Applying power settings...\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}$settings = @(");

        for (int i = 0; i < powerSettings.Count; i++)
        {
            var setting = powerSettings[i];
            var escapedDescription = EscapePowerShellString(setting.Description);
            var comma = i < powerSettings.Count - 1 ? "," : "";
            sb.AppendLine($"{indent}    @{{ S=\"{setting.SubgroupGuid}\"; G=\"{setting.SettingGuid}\"; AC={setting.AcValue}; DC={setting.DcValue}; N=\"{escapedDescription}\" }}{comma}");
        }

        sb.AppendLine($"{indent})");
        sb.AppendLine();

        var targetGuid = !string.IsNullOrEmpty(powerPlanGuid) ? powerPlanGuid : "SCHEME_CURRENT";
        sb.AppendLine($"{indent}$appliedCount = 0");
        sb.AppendLine($"{indent}$targetPlanGuid = \"{targetGuid}\"");
        sb.AppendLine($"{indent}foreach ($setting in $settings) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        powercfg /setacvalueindex $targetPlanGuid $setting.S $setting.G $setting.AC 2>$null");
        sb.AppendLine($"{indent}        if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}            powercfg /setdcvalueindex $targetPlanGuid $setting.S $setting.G $setting.DC 2>$null");
        sb.AppendLine($"{indent}            if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}                $appliedCount++");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Applied $appliedCount power settings\" \"SUCCESS\"");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(powerPlanGuid))
        {
            sb.AppendLine($"{indent}Write-Log \"Activating power plan...\" \"INFO\"");
            sb.AppendLine($"{indent}powercfg /setactive {powerPlanGuid} 2>$null");
            sb.AppendLine($"{indent}if ($LASTEXITCODE -eq 0) {{");
            sb.AppendLine($"{indent}    Write-Log \"Power plan activated successfully\" \"SUCCESS\"");
            sb.AppendLine($"{indent}}} else {{");
            sb.AppendLine($"{indent}    Write-Log \"Failed to activate power plan\" \"WARNING\"");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }
    }

    private async Task<List<PowerSettingData>> ExtractPowerSettingsAsync(
        string activePowerPlanGuid,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        var powerSettings = new List<PowerSettingData>();

        if (!allSettings.TryGetValue(FeatureIds.Power, out var settingDefinitions))
            return powerSettings;

        bool hasBattery = await _hardwareDetectionService.HasBatteryAsync().ConfigureAwait(false);

        var bulkQueryResults = await _powerSettingsQueryService.GetAllPowerSettingsACDCAsync(activePowerPlanGuid).ConfigureAwait(false);

        foreach (var settingDef in settingDefinitions)
        {
            if (settingDef.Id == "power-plan-selection" || settingDef.PowerCfgSettings?.Any() != true)
                continue;

            if (settingDef.RequiresBattery && !hasBattery)
                continue;

            if (settingDef.RequiresBrightnessSupport)
                continue;

            foreach (var powerCfgSetting in settingDef.PowerCfgSettings)
            {
                if (!bulkQueryResults.TryGetValue(powerCfgSetting.SettingGuid, out var values))
                    continue;

                if (!values.acValue.HasValue || !values.dcValue.HasValue)
                    continue;

                powerSettings.Add(new PowerSettingData
                {
                    SubgroupGuid = powerCfgSetting.SubgroupGuid,
                    SettingGuid = powerCfgSetting.SettingGuid,
                    AcValue = values.acValue.Value,
                    DcValue = values.dcValue.Value,
                    Description = settingDef.Description
                });
            }
        }

        _logService.Log(LogLevel.Info, $"Extracted {powerSettings.Count} power settings from current system state");
        return powerSettings;
    }
}

using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

public class AutounattendScriptBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogService _logService;
    private readonly IComboBoxResolver _comboBoxResolver;

    private class PowerSettingData
    {
        public string SubgroupGuid { get; set; } = string.Empty;
        public string SettingGuid { get; set; } = string.Empty;
        public int AcValue { get; set; }
        public int DcValue { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public AutounattendScriptBuilder(IServiceProvider serviceProvider, ILogService logService, IComboBoxResolver comboBoxResolver)
    {
        _serviceProvider = serviceProvider;
        _logService = logService;
        _comboBoxResolver = comboBoxResolver;
    }

    public async Task<string> BuildWinhancementsScriptAsync(
        UnifiedConfigurationFile config,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        var sb = new StringBuilder();

        // 1. Header and setup
        AppendHeader(sb);
        AppendLoggingSetup(sb);
        AppendHelperFunctions(sb);

        // 2. Build if (-not $UserCustomizations) block
        sb.AppendLine();
        sb.AppendLine("if (-not $UserCustomizations) {");
        sb.AppendLine();

        AppendScriptsDirectorySetup(sb, "    ");

        if (config.WindowsApps.Items.Any())
        {
            await AppendBloatRemovalScriptAsync(sb, config.WindowsApps.Items, "    ");
        }

        AppendWinhanceInstallerScriptContent(sb, "    ");

        // 2b. Power settings
        var powerPlanSetting = FindPowerPlanSetting(config, allSettings);
        var powerSettingsQueryService = _serviceProvider.GetRequiredService<IPowerSettingsQueryService>();
        var activePowerPlan = await powerSettingsQueryService.GetActivePowerPlanAsync();
        var powerSettings = await ExtractPowerSettingsAsync(activePowerPlan.Guid, allSettings);
        if (powerPlanSetting != null || powerSettings.Any())
        {
            AppendPowerSettingsSection(sb, powerPlanSetting, powerSettings, "    ");
        }

        // 2c. HKLM registry entries from Optimize
        if (config.Optimize.Features.Any())
        {
            AppendFeatureGroupRegistryEntries(sb, config.Optimize, allSettings, "Optimize", isHkcu: false, indent: "    ");
        }

        // 2d. HKLM registry entries from Customize
        if (config.Customize.Features.Any())
        {
            AppendFeatureGroupRegistryEntries(sb, config.Customize, allSettings, "Customize", isHkcu: false, indent: "    ");
        }

        // 2e. Clean Start Menu Layout (always included)
        AppendCleanStartMenuSection(sb, "    ");

        // 2f. Register UserCustomizations scheduled task
        AppendUserCustomizationsScheduledTask(sb, "    ");

        // 2g. System-wide custom script placeholder
        sb.AppendLine();
        sb.AppendLine("    # ============================================================================");
        sb.AppendLine("    # ADD YOUR SYSTEM WIDE POWERSHELL SCRIPT CONTENTS BELOW");
        sb.AppendLine("    # ============================================================================");
        sb.AppendLine();
        sb.AppendLine("    # Start here");
        sb.AppendLine();
        sb.AppendLine("    # End here");
        sb.AppendLine();

        sb.AppendLine("}");
        sb.AppendLine();

        // 3. Build if ($UserCustomizations) block
        sb.AppendLine("if ($UserCustomizations) {");
        sb.AppendLine();
        sb.AppendLine("    $runningAsSystem = ($env:USERNAME -eq \"SYSTEM\" -or $env:USERPROFILE -like \"*\\system32\\config\\systemprofile\")");
        sb.AppendLine("    $targetUserSID = $null");
        sb.AppendLine();
        sb.AppendLine("    if ($runningAsSystem) {");
        sb.AppendLine("        Write-Log \"UserCustomizations running as SYSTEM, detecting logged-in user...\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine("        if (-not (Test-Path \"HKU:\\\")) {");
        sb.AppendLine("            New-PSDrive -PSProvider Registry -Name HKU -Root HKEY_USERS -ErrorAction SilentlyContinue | Out-Null");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        $targetUser = Get-TargetUser");
        sb.AppendLine("        if ($targetUser) {");
        sb.AppendLine("            $targetUserSID = Get-UserSID -Username $targetUser");
        sb.AppendLine("            if ($targetUserSID) {");
        sb.AppendLine("                Write-Log \"Target user: $targetUser (SID: $targetUserSID)\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine("                Remove-PSDrive -Name HKCU -ErrorAction SilentlyContinue");
        sb.AppendLine("                New-PSDrive -PSProvider Registry -Name HKCU -Root \"HKEY_USERS\\$targetUserSID\" -ErrorAction Stop | Out-Null");
        sb.AppendLine("                Write-Log \"Remapped HKCU to target user's registry hive\" \"INFO\"");
        sb.AppendLine("            } else {");
        sb.AppendLine("                Write-Log \"Failed to get SID for user: $targetUser\" \"ERROR\"");
        sb.AppendLine("                exit 1");
        sb.AppendLine("            }");
        sb.AppendLine("        } else {");
        sb.AppendLine("            Write-Log \"No logged-in user detected, UserCustomizations cannot proceed\" \"WARNING\"");
        sb.AppendLine("            exit 1");
        sb.AppendLine("        }");
        sb.AppendLine("    } else {");
        sb.AppendLine("        Write-Log \"UserCustomizations running as user\" \"INFO\"");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    $markerPath = \"HKCU:\\Software\\Winhance\"");
        sb.AppendLine("    $markerName = \"UserCustomizationsApplied\"");
        sb.AppendLine("    $alreadyApplied = $false");
        sb.AppendLine();
        sb.AppendLine("    try {");
        sb.AppendLine("        if (Test-Path $markerPath) {");
        sb.AppendLine("            $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue");
        sb.AppendLine("            if ($value.$markerName -eq 1) {");
        sb.AppendLine("                $alreadyApplied = $true");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    } catch { }");
        sb.AppendLine();
        sb.AppendLine("    if ($alreadyApplied) {");
        sb.AppendLine("        Write-Log \"User customizations have already been applied for this user\" \"INFO\"");
        sb.AppendLine("        Write-Log \"To re-apply these settings, delete the registry value: $markerPath\\$markerName\" \"INFO\"");
        sb.AppendLine("    } else {");
        sb.AppendLine("        Write-Log \"Applying user customizations for the first time...\" \"INFO\"");
        sb.AppendLine();

        // 3a. HKCU registry entries from Optimize
        if (config.Optimize.Features.Any())
        {
            AppendFeatureGroupRegistryEntries(sb, config.Optimize, allSettings, "Optimize", isHkcu: true, indent: "        ");
        }

        // 3b. HKCU registry entries from Customize
        if (config.Customize.Features.Any())
        {
            AppendFeatureGroupRegistryEntries(sb, config.Customize, allSettings, "Customize", isHkcu: true, indent: "        ");
        }

        // 3c. User-specific custom script placeholder
        sb.AppendLine();
        sb.AppendLine("        # ============================================================================");
        sb.AppendLine("        # ADD YOUR USER SPECIFIC POWERSHELL SCRIPT CONTENTS BELOW");
        sb.AppendLine("        # ============================================================================");
        sb.AppendLine();
        sb.AppendLine("        # Start here");
        sb.AppendLine();
        sb.AppendLine("        # End here");
        sb.AppendLine();

        sb.AppendLine();
        sb.AppendLine("        try {");
        sb.AppendLine("            if (-not (Test-Path $markerPath)) {");
        sb.AppendLine("                New-Item -Path $markerPath -Force | Out-Null");
        sb.AppendLine("            }");
        sb.AppendLine("            Set-ItemProperty -Path $markerPath -Name $markerName -Value 1 -Type DWord -Force");
        sb.AppendLine("            Write-Log \"User customizations completed and marked as applied\" \"SUCCESS\"");
        sb.AppendLine("            Write-Log \"Note: User customizations will not run again unless $markerPath\\$markerName is deleted\" \"INFO\"");
        sb.AppendLine("        } catch {");
        sb.AppendLine("            Write-Log \"Failed to create completion marker: $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if ($runningAsSystem -and $targetUserSID) {");
        sb.AppendLine("        try {");
        sb.AppendLine("            Remove-PSDrive -Name HKCU -ErrorAction SilentlyContinue");
        sb.AppendLine("            New-PSDrive -PSProvider Registry -Name HKCU -Root HKEY_CURRENT_USER -ErrorAction SilentlyContinue | Out-Null");
        sb.AppendLine("            Write-Log \"Restored HKCU PSDrive\" \"INFO\"");
        sb.AppendLine("        } catch { }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (-not $alreadyApplied) {");
        sb.AppendLine("        Write-Log \"Rebooting system to apply user customizations...\" \"INFO\"");
        sb.AppendLine("        # Wait 20 seconds to give the FirstLogon phase some more time before restarting");
        sb.AppendLine("        shutdown.exe /r /t 20");
        sb.AppendLine("    } else {");
        sb.AppendLine("        Write-Log \"No restart needed - customizations were already applied\" \"INFO\"");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // 4. Completion block
        AppendCompletionBlock(sb);

        var scriptContent = sb.ToString();

        // Validate the generated script has no PowerShell syntax errors
        try
        {
            await PowerShellRunner.ValidateScriptSyntaxAsync(scriptContent);
            _logService.Log(LogLevel.Info, "Winhancements.ps1 script passed PowerShell syntax validation");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Winhancements.ps1 script failed PowerShell syntax validation: {ex.Message}");
            throw;
        }

        return scriptContent;
    }

    private ConfigurationItem? FindPowerPlanSetting(
        UnifiedConfigurationFile config,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        if (!config.Optimize.Features.TryGetValue(FeatureIds.Power, out var powerSection))
            return null;

        return powerSection.Items.FirstOrDefault(item =>
            item.Id == "power-plan-selection" && !string.IsNullOrEmpty(item.PowerPlanGuid));
    }

    private async Task<List<PowerSettingData>> ExtractPowerSettingsAsync(
        string activePowerPlanGuid,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        var powerSettings = new List<PowerSettingData>();

        if (!allSettings.TryGetValue(FeatureIds.Power, out var settingDefinitions))
            return powerSettings;

        var hardwareService = _serviceProvider.GetRequiredService<IHardwareDetectionService>();
        var powerSettingsQueryService = _serviceProvider.GetRequiredService<IPowerSettingsQueryService>();

        bool hasBattery = await hardwareService.HasBatteryAsync();

        var bulkQueryResults = await powerSettingsQueryService.GetAllPowerSettingsACDCAsync(activePowerPlanGuid);

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

    private void AppendFeatureGroupRegistryEntries(
        StringBuilder sb,
        FeatureGroupSection featureGroup,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings,
        string groupName,
        bool isHkcu,
        string indent)
    {
        foreach (var featureKvp in featureGroup.Features)
        {
            var featureId = featureKvp.Key;
            var configSection = featureKvp.Value;

            if (!allSettings.TryGetValue(featureId, out var settingDefinitions))
            {
                _logService.Log(LogLevel.Warning, $"Could not find SettingDefinitions for feature: {featureId}");
                continue;
            }

            bool hasEntriesForCurrentHive = false;
            foreach (var configItem in configSection.Items)
            {
                var settingDef = settingDefinitions.FirstOrDefault(s => s.Id == configItem.Id);
                if (settingDef == null) continue;

                if (settingDef.Id == "power-plan-selection") continue;

                foreach (var regSetting in settingDef.RegistrySettings)
                {
                    bool isHkcuEntry = regSetting.KeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
                    if (isHkcuEntry == isHkcu)
                    {
                        hasEntriesForCurrentHive = true;
                        break;
                    }
                }

                if (!isHkcu && settingDef.ScheduledTaskSettings?.Count > 0)
                {
                    hasEntriesForCurrentHive = true;
                }

                if (!isHkcu && settingDef.Id == "power-hibernation-enable")
                {
                    hasEntriesForCurrentHive = true;
                }

                if (hasEntriesForCurrentHive) break;
            }

            if (!hasEntriesForCurrentHive) continue;

            // Get the feature display name for the section header
            var featureDisplayName = GetFeatureDisplayName(featureId);

            sb.AppendLine();
            sb.AppendLine($"{indent}# ============================================================================");
            sb.AppendLine($"{indent}# {featureDisplayName.ToUpper()}");
            sb.AppendLine($"{indent}# ============================================================================");
            sb.AppendLine();

            // Process each setting in the feature
            foreach (var configItem in configSection.Items)
            {
                var settingDef = settingDefinitions.FirstOrDefault(s => s.Id == configItem.Id);
                if (settingDef == null)
                {
                    _logService.Log(LogLevel.Warning, $"Could not find SettingDefinition for: {configItem.Id}");
                    continue;
                }

                // Skip settings that have PowerCfgSettings but no RegistrySettings (already handled in Power Settings section)
                if (settingDef.PowerCfgSettings?.Any() == true && settingDef.RegistrySettings?.Any() != true)
                    continue;

                // Apply the setting, but only output registry entries that match the current hive
                if (configItem.InputType == InputType.Toggle)
                {
                    AppendToggleCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
                }
                else if (configItem.InputType == InputType.Selection)
                {
                    AppendSelectionCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
                }
            }

            if (!isHkcu)
            {
                var scheduledTasksToApply = new List<(string TaskName, string Action, string Description)>();

                foreach (var configItem in configSection.Items)
                {
                    var settingDef = settingDefinitions.FirstOrDefault(s => s.Id == configItem.Id);
                    if (settingDef?.ScheduledTaskSettings?.Count > 0)
                    {
                        foreach (var taskSetting in settingDef.ScheduledTaskSettings)
                        {
                            var action = configItem.IsSelected == true ? "/Enable" : "/Disable";
                            scheduledTasksToApply.Add((taskSetting.TaskPath, action, settingDef.Description));
                        }
                    }

                    if (settingDef?.Id == "power-hibernation-enable")
                    {
                        var hibernateState = configItem.IsSelected == true ? "on" : "off";
                        sb.AppendLine();
                        sb.AppendLine($"{indent}Write-Log \"Setting hibernation to {hibernateState}...\" \"INFO\"");
                        sb.AppendLine($"{indent}powercfg /hibernate {hibernateState} 2>$null");
                        sb.AppendLine($"{indent}Write-Log \"Hibernation set to {hibernateState}\" \"SUCCESS\"");
                    }
                }

                if (scheduledTasksToApply.Any())
                {
                    AppendScheduledTaskBatch(sb, scheduledTasksToApply, indent);
                }
            }

            if (featureId == FeatureIds.WindowsTheme && isHkcu)
            {
                AppendWallpaperSetting(sb, indent);
            }

            if (featureId == FeatureIds.Update && !isHkcu)
            {
                var updatePolicySetting = configSection.Items.FirstOrDefault(i => i.Id == "updates-policy-mode");
                if (updatePolicySetting?.SelectedIndex == 3)
                {
                    AppendWindowsUpdateDisabledModeLogic(sb, indent);
                }
            }
        }
    }

    private void AppendWallpaperSetting(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Setting wallpaper based on Windows version and theme...\" \"INFO\"");
        sb.AppendLine($"{indent}$buildNumber = [System.Environment]::OSVersion.Version.Build");
        sb.AppendLine($"{indent}$wallpaperPath = $null");
        sb.AppendLine();
        sb.AppendLine($"{indent}if ($buildNumber -ge 22000) {{");
        sb.AppendLine($"{indent}    $themeKey = 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize'");
        sb.AppendLine($"{indent}    $lightTheme = $false");
        sb.AppendLine();
        sb.AppendLine($"{indent}    if (Test-Path $themeKey) {{");
        sb.AppendLine($"{indent}        $value = Get-ItemProperty -Path $themeKey -Name 'SystemUsesLightTheme' -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        if ($value.SystemUsesLightTheme -eq 1) {{");
        sb.AppendLine($"{indent}            $lightTheme = $true");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    if ($lightTheme) {{");
        sb.AppendLine($"{indent}        $wallpaperPath = 'C:\\Windows\\Web\\Wallpaper\\Windows\\img0.jpg'");
        sb.AppendLine($"{indent}    }} else {{");
        sb.AppendLine($"{indent}        $wallpaperPath = 'C:\\Windows\\Web\\Wallpaper\\Windows\\img19.jpg'");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    $wallpaperPath = 'C:\\Windows\\Web\\4K\\Wallpaper\\Windows\\img0_3840x2160.jpg'");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}if (-not (Test-Path $wallpaperPath)) {{");
        sb.AppendLine($"{indent}    Write-Log \"Wallpaper file not found: $wallpaperPath\" \"WARNING\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $desktopKey = 'HKCU:\\Control Panel\\Desktop'");
        sb.AppendLine($"{indent}        Set-ItemProperty -Path $desktopKey -Name Wallpaper -Value $wallpaperPath -Type String -Force");
        sb.AppendLine($"{indent}        Set-ItemProperty -Path $desktopKey -Name WallpaperStyle -Value '10' -Type String -Force");
        sb.AppendLine($"{indent}        Set-ItemProperty -Path $desktopKey -Name TileWallpaper -Value '0' -Type String -Force");
        sb.AppendLine();
        sb.AppendLine($"{indent}        Remove-ItemProperty -Path $desktopKey -Name 'TranscodedImageCache' -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        Remove-ItemProperty -Path $desktopKey -Name 'TranscodedImageCache_000' -ErrorAction SilentlyContinue");
        sb.AppendLine();
        sb.AppendLine($"{indent}        Write-Log \"Wallpaper configured: $wallpaperPath\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to set wallpaper: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendCompletionBlock(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("Write-Log \"================================================================================\" \"INFO\"");
        sb.AppendLine("Write-Log \"Winhance Windows Optimization & Customization Script Completed\" \"SUCCESS\"");
        sb.AppendLine("Write-Log \"================================================================================\" \"INFO\"");
    }

    private string GetFeatureDisplayName(string featureId)
    {
        var definition = FeatureDefinitions.Get(featureId);
        return definition != null ? $"{definition.DefaultName} Settings" : $"{featureId} Settings";
    }

    private void AppendToggleCommandsFiltered(StringBuilder sb, SettingDefinition setting, ConfigurationItem configItem, bool isHkcu, string indent = "")
    {
        var escapedDescription = EscapePowerShellString(setting.Description);
        var isEnabled = configItem.IsSelected;

        foreach (var regSetting in setting.RegistrySettings)
        {
            // Filter by hive
            bool isHkcuEntry = regSetting.KeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
            if (isHkcuEntry != isHkcu)
                continue;

            var regPath = EscapePowerShellString(ConvertRegistryPath(regSetting.KeyPath));
            var escapedValueName = EscapePowerShellString(regSetting.ValueName);

            // Check if we have a raw value from the registry to use instead of definitions
            var key = regSetting.ValueName ?? "KeyExists";
            object? customValue = null;
            bool hasCustomValue = configItem.CustomStateValues?.TryGetValue(key, out customValue) == true;

            // Pattern 1: Key-Based Settings (CLSID folders, etc.)
            // Detection: ValueName is null or empty - these control registry KEY existence, not values
            if (string.IsNullOrEmpty(regSetting.ValueName))
            {
                var keyValue = isEnabled == true ? regSetting.EnabledValue : regSetting.DisabledValue;

                if (keyValue == null)
                {
                    // Value is null = key should NOT exist in this state
                    sb.AppendLine($"{indent}Remove-RegistryKey -Path '{regPath}' -Description '{escapedDescription}'");
                }
                else if (keyValue is string keyStrValue && keyStrValue == "")
                {
                    // Empty string = key SHOULD exist with default value set to empty string
                    sb.AppendLine($"{indent}New-RegistryKey -Path '{regPath}' -Description '{escapedDescription}'");
                    sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '(Default)' -Type 'String' -Value '' -Description '{escapedDescription}'");
                }
                else
                {
                    // Value is non-null = key SHOULD exist in this state
                    sb.AppendLine($"{indent}New-RegistryKey -Path '{regPath}' -Description '{escapedDescription}'");
                }
                continue;
            }

            if (hasCustomValue)
            {
                if (customValue == null)
                {
                    // Value doesn't exist in registry, skip adding it
                    continue;
                }

                // Use the exact value found in registry
                var valueType = ConvertToRegistryType(regSetting.ValueType);

                if (regSetting.ValueType == RegistryValueKind.Binary && regSetting.BinaryByteIndex.HasValue)
                {
                    if (regSetting.BitMask.HasValue)
                    {
                        var setBit = Convert.ToBoolean(customValue);
                        sb.AppendLine($"{indent}Set-BinaryBit -Path '{regPath}' -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -BitMask 0x{regSetting.BitMask.Value:X2} -SetBit ${setBit} -Description '{escapedDescription}'");
                    }
                    else if (regSetting.ModifyByteOnly)
                    {
                        var byteValue = FormatValueForPowerShell(customValue, RegistryValueKind.DWord).Replace("0x", "").PadLeft(2, '0');
                        sb.AppendLine($"{indent}Set-BinaryByte -Path '{regPath}' -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -ByteValue 0x{byteValue} -Description '{escapedDescription}'");
                    }
                    else
                    {
                        var formattedValue = FormatValueForPowerShell(customValue, regSetting.ValueType);
                        sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
                    }
                }
                else
                {
                    var formattedValue = FormatValueForPowerShell(customValue, regSetting.ValueType);
                    sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
                }
                continue;
            }

            // Fallback for when custom value is not available (should happen rarely if discovery worked)
            var value = isEnabled == true ? regSetting.EnabledValue : regSetting.DisabledValue;

            if (value is string strValue && strValue == "")
            {
                sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type 'String' -Value '' -Description '{escapedDescription}'");
                continue;
            }

            // Pattern 3: Null Value Deletion
            if (value == null)
            {
                sb.AppendLine($"{indent}Remove-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Description '{escapedDescription}'");
                continue;
            }

            // Pattern 4: Regular Value Setting
            var fallbackValueType = ConvertToRegistryType(regSetting.ValueType);

            if (regSetting.ValueType == RegistryValueKind.Binary && regSetting.BinaryByteIndex.HasValue)
            {
                if (regSetting.BitMask.HasValue)
                {
                    var setBit = isEnabled == true;
                    sb.AppendLine($"{indent}Set-BinaryBit -Path '{regPath}' -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -BitMask 0x{regSetting.BitMask.Value:X2} -SetBit ${setBit} -Description '{escapedDescription}'");
                }
                else if (regSetting.ModifyByteOnly)
                {
                    var byteValue = value switch
                    {
                        byte b => $"0x{b:X2}",
                        int i => $"0x{(byte)i:X2}",
                        _ => "0x00"
                    };
                    sb.AppendLine($"{indent}Set-BinaryByte -Path '{regPath}' -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -ByteValue {byteValue} -Description '{escapedDescription}'");
                }
                else
                {
                    var formattedValue = FormatValueForPowerShell(value, regSetting.ValueType);
                    sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{fallbackValueType}' -Value {formattedValue} -Description '{escapedDescription}'");
                }
            }
            else
            {
                var formattedValue = FormatValueForPowerShell(value, regSetting.ValueType);
                sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{fallbackValueType}' -Value {formattedValue} -Description '{escapedDescription}'");
            }
        }

        if (setting.RegContents?.Count > 0)
        {
            AppendRegContentCommands(sb, setting, isEnabled, isHkcu, indent);
        }
    }

    private void AppendRegContentCommands(StringBuilder sb, SettingDefinition setting, bool? isEnabled, bool isHkcuPass, string indent = "")
    {
        if (setting.RegContents?.Count == 0) return;

        var escapedDescription = EscapePowerShellString(setting.Description);
        var varName = SanitizeVariableName(setting.Id);

        foreach (var regContent in setting.RegContents!)
        {
            var content = isEnabled == true ? regContent.EnabledContent : regContent.DisabledContent;

            if (string.IsNullOrEmpty(content)) continue;

            // Determine if this content belongs in the current pass (System vs User)
            // We check for HKEY_CURRENT_USER usage to identify User-specific content
            bool isHkcuContent = content.IndexOf("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 content.IndexOf("HKCU", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isHkcuContent != isHkcuPass)
                continue;

            sb.AppendLine($"{indent}try {{");
            sb.AppendLine($"{indent}    $regContent_{varName} = @'");
            sb.AppendLine(content);
            sb.AppendLine("'@");
            sb.AppendLine($"{indent}    $tempRegFile = Join-Path $env:TEMP \"winhance_{setting.Id}_$((Get-Date).Ticks).reg\"");
            sb.AppendLine($"{indent}    $regContent_{varName} | Out-File -FilePath $tempRegFile -Encoding Unicode -Force");
            sb.AppendLine($"{indent}    reg import \"$tempRegFile\" 2>&1 | Out-Null");
            sb.AppendLine($"{indent}    if ($LASTEXITCODE -eq 0) {{");
            sb.AppendLine($"{indent}        Write-Log \"{escapedDescription}\" \"SUCCESS\"");
            sb.AppendLine($"{indent}    }} else {{");
            sb.AppendLine($"{indent}        Write-Log \"Failed to import registry content for {escapedDescription}\" \"ERROR\"");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}    Remove-Item $tempRegFile -Force -ErrorAction SilentlyContinue");
            sb.AppendLine($"{indent}}} catch {{");
            sb.AppendLine($"{indent}    Write-Log \"Error processing registry content for {escapedDescription}: $($_.Exception.Message)\" \"ERROR\"");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }
    }

    private void AppendScheduledTaskBatch(StringBuilder sb, List<(string TaskName, string Action, string Description)> tasks, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}$scheduledTasks = @(");

        for (int i = 0; i < tasks.Count; i++)
        {
            var (taskName, action, description) = tasks[i];
            var escapedTaskName = EscapePowerShellString(taskName);
            var escapedDescription = EscapePowerShellString(description);
            var comma = i < tasks.Count - 1 ? "," : "";

            sb.AppendLine($"{indent}    @{{ TN=\"{escapedTaskName}\"; Action=\"{action}\"; Desc=\"{escapedDescription}\" }}{comma}");
        }

        sb.AppendLine($"{indent})");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Applying scheduled task settings...\" \"INFO\"");
        sb.AppendLine($"{indent}$processedCount = 0");
        sb.AppendLine($"{indent}foreach ($task in $scheduledTasks) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $result = & cmd.exe /c \"schtasks /Change /TN `\"$($task.TN)`\" $($task.Action)\" 2>&1");
        sb.AppendLine($"{indent}        if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}            Write-Log \"$($task.Desc)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            $processedCount++");
        sb.AppendLine($"{indent}        }} else {{");
        sb.AppendLine($"{indent}            Write-Log \"Task command failed for: $($task.Desc)\" \"WARNING\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to process task: $($task.Desc) - $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Processed $processedCount scheduled task settings\" \"SUCCESS\"");
        sb.AppendLine();
    }

    private void AppendSelectionCommandsFiltered(StringBuilder sb, SettingDefinition setting, ConfigurationItem configItem, bool isHkcu, string indent = "")
    {
        if (setting.Id == "power-plan-selection")
            return;

        Dictionary<string, object> valuesToApply;

        if (configItem.CustomStateValues != null && configItem.CustomStateValues.Any())
        {
            valuesToApply = configItem.CustomStateValues;
        }
        else if (configItem.SelectedIndex.HasValue &&
                 setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ValueMappings) == true)
        {
            var resolvedValues = _comboBoxResolver.ResolveIndexToRawValues(setting, configItem.SelectedIndex.Value);
            valuesToApply = resolvedValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);
        }
        else
        {
            _logService.Log(LogLevel.Warning, $"Selection setting {setting.Id} has no ValueMappings or CustomStateValues");
            return;
        }

        ApplyResolvedValues(sb, setting, valuesToApply, isHkcu, indent);
    }

    private void ApplyResolvedValues(StringBuilder sb, SettingDefinition setting, Dictionary<string, object> valuesToApply, bool isHkcu, string indent)
    {
        var escapedDescription = EscapePowerShellString(setting.Description);

        foreach (var kvp in valuesToApply)
        {
            if (kvp.Key == "PowerCfgValue" && setting.PowerCfgSettings?.Any() == true)
            {
                foreach (var powerCfgSetting in setting.PowerCfgSettings)
                {
                    var value = Convert.ToInt32(kvp.Value);

                    if (powerCfgSetting.PowerModeSupport == PowerModeSupport.Separate)
                    {
                        sb.AppendLine($"{indent}powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {value}");
                        sb.AppendLine($"{indent}powercfg /setdcvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {value}");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {value}");
                    }
                }
                sb.AppendLine($"{indent}Write-Log 'Applied: {escapedDescription}' 'SUCCESS'");
                continue;
            }

            var matchingRegSettings = setting.RegistrySettings
                .Where(r => r.ValueName == kvp.Key || kvp.Key == "KeyExists")
                .ToList();

            foreach (var regSetting in matchingRegSettings)
            {
                bool isHkcuEntry = regSetting.KeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
                if (isHkcuEntry != isHkcu)
                    continue;

                var regPath = EscapePowerShellString(ConvertRegistryPath(regSetting.KeyPath));
                var escapedValueName = EscapePowerShellString(regSetting.ValueName);

                if (kvp.Value == null)
                {
                    continue;
                }
                else
                {
                    var valueType = ConvertToRegistryType(regSetting.ValueType);

                    if (regSetting.ValueType == RegistryValueKind.Binary && regSetting.BinaryByteIndex.HasValue)
                    {
                        if (regSetting.BitMask.HasValue)
                        {
                            var setBit = Convert.ToBoolean(kvp.Value);
                            sb.AppendLine($"{indent}Set-BinaryBit -Path '{regPath}' -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -BitMask 0x{regSetting.BitMask.Value:X2} -SetBit ${setBit} -Description '{escapedDescription}'");
                        }
                        else if (regSetting.ModifyByteOnly)
                        {
                            var byteValue = kvp.Value switch
                            {
                                byte b => $"0x{b:X2}",
                                int i => $"0x{(byte)i:X2}",
                                _ => "0x00"
                            };
                            sb.AppendLine($"{indent}Set-BinaryByte -Path '{regPath}' -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -ByteValue {byteValue} -Description '{escapedDescription}'");
                        }
                        else
                        {
                            var formattedValue = FormatValueForPowerShell(kvp.Value, regSetting.ValueType);
                            sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
                        }
                    }
                    else
                    {
                        var formattedValue = FormatValueForPowerShell(kvp.Value, regSetting.ValueType);
                        sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
                    }
                }
            }
        }
    }

    private void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine($@"<#
.SYNOPSIS
    Winhance Windows 10/11 Customization and Optimization Script
.DESCRIPTION
    Applies registry settings, UWP app removals, optimizations and customizations based on Windows version detection
.NOTES
    Requires Administrator privileges
    Compatible with Windows 10 and Windows 11
    Logs all activities to C:\ProgramData\Winhance\Unattend\Logs\Winhancements.txt
.PARAMETER UserCustomizations
    When specified, applies ONLY HKCU (user-specific) registry settings.
    When not specified, applies all settings EXCEPT HKCU entries.
    Note: User customizations are tracked and will only apply once per user.
    To re-apply, delete: HKCU\Software\Winhance\UserCustomizationsApplied
.EXAMPLE
    .\Winhancements.ps1
    Runs in normal mode - applies all system-wide settings (HKLM) but skips user settings (HKCU)
.EXAMPLE
    .\Winhancements.ps1 -UserCustomizations
    Runs in user mode - applies ONLY user-specific settings (HKCU)
#>

param(
    [switch]$UserCustomizations
)");
    }

    private void AppendLoggingSetup(StringBuilder sb)
    {
        sb.AppendLine(@"
# ============================================================================
# LOGGING SETUP
# ============================================================================

$LogPath = 'C:\ProgramData\Winhance\Unattend\Logs\Winhancements.txt'
$null = New-Item -Path (Split-Path $LogPath) -ItemType Directory -Force

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet(""INFO"", ""SUCCESS"", ""WARNING"", ""ERROR"")]
        [string]$Level = ""INFO""
    )
    
    $Timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    $LogEntry = ""[$Timestamp] [$Level] $Message""
    
    # Write to log file
    Add-Content -Path $LogPath -Value $LogEntry -Encoding UTF8
    
    # Optional: Also write to console for real-time monitoring
    # Uncomment the next line if you want console output during testing
    # Write-Host $LogEntry
}

# Initialize log file
Write-Log ""================================================================================="" ""INFO""
Write-Log ""Winhance Windows Optimization & Customization Script Started"" ""INFO""
Write-Log ""Script Path: $($MyInvocation.MyCommand.Path)"" ""INFO""
Write-Log ""Log File: $LogPath"" ""INFO""
if ($UserCustomizations) {
    Write-Log ""MODE: User Customizations Only (HKCU registry entries)"" ""INFO""
} else {
    Write-Log ""MODE: System Customizations (All settings except HKCU entries)"" ""INFO""
}
Write-Log ""================================================================================="" ""INFO""
");
    }

    private void AppendHelperFunctions(StringBuilder sb)
    {
        sb.AppendLine(@"
function Get-TargetUser {
    try {
        $user = Get-WmiObject Win32_ComputerSystem | Select-Object -ExpandProperty UserName
        if ($user -and $user -ne ""NT AUTHORITY\SYSTEM"") {
            $username = $user.Split('\')[1]
            if ($username -ne ""defaultuser0"") {
                return $username
            }
        }
    } catch { }

    try {
        $explorer = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($explorer) {
            $owner = $explorer.GetOwner()
            if ($owner.User -ne ""defaultuser0"") {
                return $owner.User
            }
        }
    } catch { }

    return $null
}

function Get-UserSID {
    param($Username)
    try {
        $user = New-Object System.Security.Principal.NTAccount($Username)
        return $user.Translate([System.Security.Principal.SecurityIdentifier]).Value
    } catch {
        return $null
    }
}

function Set-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Type,
        $Value,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }
        Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type -Force
        Write-Log ""$Description | $Path\$Name = $Value"" ""SUCCESS""
    }
    catch {
        Write-Log ""Failed to set $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}

function Remove-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Description
    )

    try {
        if (Test-Path $Path) {
            $existingValue = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
            if ($existingValue) {
                Remove-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
                Write-Log ""$Description | Removed $Path\$Name"" ""SUCCESS""
            }
        }
    }
    catch {
        Write-Log ""Failed to remove $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}

function Remove-RegistryKey {
    param(
        [string]$Path,
        [string]$Description
    )

    try {
        if (Test-Path $Path) {
            Remove-Item -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
            Write-Log ""$Description | Removed key $Path"" ""SUCCESS""
        }
    }
    catch {
        Write-Log ""Failed to remove key $Path : $($_.Exception.Message)"" ""ERROR""
    }
}

function New-RegistryKey {
    param(
        [string]$Path,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
            Write-Log ""$Description | Created key $Path"" ""SUCCESS""
        }
    }
    catch {
        Write-Log ""Failed to create key $Path : $($_.Exception.Message)"" ""ERROR""
    }
}

function Set-BinaryBit {
    param(
        [string]$Path,
        [string]$Name,
        [int]$ByteIndex,
        [byte]$BitMask,
        [bool]$SetBit,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }

        $currentValue = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        if ($null -eq $currentValue -or $null -eq $currentValue.$Name) {
            $bytes = New-Object byte[] ([Math]::Max(12, $ByteIndex + 1))
        } else {
            $bytes = $currentValue.$Name
            if ($bytes.Length -le $ByteIndex) {
                $newBytes = New-Object byte[] ($ByteIndex + 1)
                [Array]::Copy($bytes, $newBytes, $bytes.Length)
                $bytes = $newBytes
            }
        }

        if ($SetBit) {
            $bytes[$ByteIndex] = $bytes[$ByteIndex] -bor $BitMask
        } else {
            $bytes[$ByteIndex] = $bytes[$ByteIndex] -band (-bnot $BitMask)
        }

        Set-ItemProperty -Path $Path -Name $Name -Value $bytes -Type Binary -Force
        Write-Log ""$Description | $Path\$Name bit mask 0x$($BitMask.ToString('X2')) at byte $ByteIndex = $SetBit"" ""SUCCESS""
    }
    catch {
        Write-Log ""Failed to modify binary bit $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}

function Set-BinaryByte {
    param(
        [string]$Path,
        [string]$Name,
        [int]$ByteIndex,
        [byte]$ByteValue,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }

        $currentValue = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        if ($null -eq $currentValue -or $null -eq $currentValue.$Name) {
            $bytes = New-Object byte[] ([Math]::Max(12, $ByteIndex + 1))
        } else {
            $bytes = $currentValue.$Name
            if ($bytes.Length -le $ByteIndex) {
                $newBytes = New-Object byte[] ($ByteIndex + 1)
                [Array]::Copy($bytes, $newBytes, $bytes.Length)
                $bytes = $newBytes
            }
        }

        $bytes[$ByteIndex] = $ByteValue
        Set-ItemProperty -Path $Path -Name $Name -Value $bytes -Type Binary -Force
        Write-Log ""$Description | $Path\$Name byte $ByteIndex = 0x$($ByteValue.ToString('X2'))"" ""SUCCESS""
    }
    catch {
        Write-Log ""Failed to modify binary byte $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}
");
    }

    private void AppendScriptsDirectorySetup(StringBuilder sb, string indent = "")
    {
        sb.AppendLine($"{indent}$scriptsDir = \"C:\\ProgramData\\Winhance\\Scripts\"");
        sb.AppendLine($"{indent}if (!(Test-Path $scriptsDir)) {{");
        sb.AppendLine($"{indent}    New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null");
        sb.AppendLine($"{indent}    Write-Log \"Created scripts directory: $scriptsDir\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    Write-Log \"Scripts directory already exists: $scriptsDir\" \"INFO\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private async Task AppendBloatRemovalScriptAsync(StringBuilder sb, List<ConfigurationItem> selectedApps, string indent = "")
    {
        // Categorize apps by type
        var regularApps = new List<string>();
        var capabilities = new List<string>();
        var optionalFeatures = new List<string>();
        var specialApps = new List<string>();
        var edgeRemovalNeeded = false;
        var oneDriveRemovalNeeded = false;

        foreach (var app in selectedApps)
        {
            // Check for special apps that need dedicated scripts
            if (app.Id == "windows-app-edge")
            {
                edgeRemovalNeeded = true;
                continue;
            }

            if (app.Id == "windows-app-onedrive")
            {
                oneDriveRemovalNeeded = true;
                continue;
            }

            // Categorize apps by their specific property
            if (!string.IsNullOrEmpty(app.CapabilityName))
            {
                capabilities.Add(app.CapabilityName);
            }
            else if (!string.IsNullOrEmpty(app.OptionalFeatureName))
            {
                optionalFeatures.Add(app.OptionalFeatureName);
            }
            else if (!string.IsNullOrEmpty(app.AppxPackageName))
            {
                regularApps.Add(app.AppxPackageName);

                if (app.SubPackages?.Length > 0)
                {
                    regularApps.AddRange(app.SubPackages);
                }

                if (app.AppxPackageName.Contains("OneNote", StringComparison.OrdinalIgnoreCase) &&
                    !specialApps.Contains("OneNote"))
                {
                    specialApps.Add("OneNote");
                }
            }
        }

        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# WINDOWS APPS REMOVAL");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();

        // Embed BloatRemoval.ps1 if there are regular apps to remove
        if (regularApps.Any() || capabilities.Any() || optionalFeatures.Any() || specialApps.Any())
        {
            AppendBloatRemovalScriptContent(sb, regularApps, capabilities, optionalFeatures, specialApps, indent);
        }

        // Embed EdgeRemoval.ps1 if needed
        if (edgeRemovalNeeded)
        {
            AppendEdgeRemovalScriptContent(sb, indent);
        }

        // Embed OneDriveRemoval.ps1 if needed
        if (oneDriveRemovalNeeded)
        {
            AppendOneDriveRemovalScriptContent(sb, indent);
        }

        // Execute the scripts and register scheduled tasks
        sb.AppendLine();
        sb.AppendLine($"{indent}# Execute removal scripts and register scheduled tasks");
        sb.AppendLine($"{indent}$scriptsToExecute = @()");

        if (regularApps.Any() || capabilities.Any() || optionalFeatures.Any() || specialApps.Any())
        {
            sb.AppendLine($"{indent}$scriptsToExecute += @{{Path = \"$scriptsDir\\BloatRemoval.ps1\"; Name = \"BloatRemoval\"; TriggerType = \"Logon\"}}");
        }

        if (edgeRemovalNeeded)
        {
            sb.AppendLine($"{indent}$scriptsToExecute += @{{Path = \"$scriptsDir\\EdgeRemoval.ps1\"; Name = \"EdgeRemoval\"; TriggerType = \"Startup\"}}");
        }

        if (oneDriveRemovalNeeded)
        {
            sb.AppendLine($"{indent}$scriptsToExecute += @{{Path = \"$scriptsDir\\OneDriveRemoval.ps1\"; Name = \"OneDriveRemoval\"; TriggerType = \"Logon\"}}");
        }

        sb.AppendLine();
        sb.AppendLine($"{indent}foreach ($script in $scriptsToExecute) {{");
        sb.AppendLine($"{indent}    if (Test-Path $script.Path) {{");
        sb.AppendLine($"{indent}        Write-Log \"Executing $($script.Name) script...\" \"INFO\"");
        sb.AppendLine($"{indent}        try {{");
        sb.AppendLine($"{indent}            Start-Process powershell.exe -ArgumentList \"-ExecutionPolicy Bypass -NoProfile -File `\"$($script.Path)`\"\" -Wait -NoNewWindow");
        sb.AppendLine($"{indent}            Write-Log \"$($script.Name) execution completed\" \"SUCCESS\"");
        sb.AppendLine($"{indent}        }} catch {{");
        sb.AppendLine($"{indent}            Write-Log \"$($script.Name) execution failed: $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        # Register scheduled task");
        sb.AppendLine($"{indent}        Write-Log \"Registering scheduled task for $($script.Name)...\" \"INFO\"");
        sb.AppendLine($"{indent}        try {{");
        sb.AppendLine($"{indent}            $action = New-ScheduledTaskAction -Execute \"powershell.exe\" -Argument \"-ExecutionPolicy Bypass -NoProfile -File `\"$($script.Path)`\"\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}            if ($script.TriggerType -eq \"Startup\") {{");
        sb.AppendLine($"{indent}                $trigger = New-ScheduledTaskTrigger -AtStartup");
        sb.AppendLine($"{indent}            }} else {{");
        sb.AppendLine($"{indent}                $trigger = New-ScheduledTaskTrigger -AtLogon");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}            $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0");
        sb.AppendLine($"{indent}            $principal = New-ScheduledTaskPrincipal -UserId \"SYSTEM\" -LogonType ServiceAccount -RunLevel Highest");
        sb.AppendLine();
        sb.AppendLine($"{indent}            Register-ScheduledTask -TaskName $script.Name -TaskPath \"\\Winhance\" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null");
        sb.AppendLine($"{indent}            Write-Log \"Registered scheduled task: $($script.Name)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}        }} catch {{");
        sb.AppendLine($"{indent}            Write-Log \"Failed to register task $($script.Name): $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Windows Apps removal configuration completed\" \"SUCCESS\"");
    }

    private void AppendBloatRemovalScriptContent(StringBuilder sb, List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps, string indent = "")
    {
        sb.AppendLine($"{indent}# Create BloatRemoval.ps1 script");
        sb.AppendLine($"{indent}$bloatRemovalContent = @'");

        // Get the BloatRemoval script template from BloatRemovalService
        // We'll generate it dynamically like Winhance does
        sb.Append(GenerateBloatRemovalScriptContent(packages, capabilities, optionalFeatures, specialApps));

        sb.AppendLine("'@");
        sb.AppendLine();
        sb.AppendLine($"{indent}$bloatRemovalPath = Join-Path $scriptsDir \"BloatRemoval.ps1\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $bloatRemovalContent | Out-File -FilePath $bloatRemovalPath -Encoding UTF8 -Force");
        sb.AppendLine($"{indent}    Write-Log \"Created: BloatRemoval.ps1\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to create BloatRemoval.ps1: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendEdgeRemovalScriptContent(StringBuilder sb, string indent = "")
    {
        sb.AppendLine($"{indent}# Create EdgeRemoval.ps1 script");
        sb.AppendLine($"{indent}$edgeRemovalContent = @'");
        sb.Append(EdgeRemovalScript.GetScript());
        sb.AppendLine("'@");
        sb.AppendLine();
        sb.AppendLine($"{indent}$edgeRemovalPath = Join-Path $scriptsDir \"EdgeRemoval.ps1\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $edgeRemovalContent | Out-File -FilePath $edgeRemovalPath -Encoding UTF8 -Force");
        sb.AppendLine($"{indent}    Write-Log \"Created: EdgeRemoval.ps1\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to create EdgeRemoval.ps1: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendOneDriveRemovalScriptContent(StringBuilder sb, string indent = "")
    {
        sb.AppendLine($"{indent}# Create OneDriveRemoval.ps1 script");
        sb.AppendLine($"{indent}$oneDriveRemovalContent = @'");
        sb.Append(OneDriveRemovalScript.GetScript());
        sb.AppendLine("'@");
        sb.AppendLine();
        sb.AppendLine($"{indent}$oneDriveRemovalPath = Join-Path $scriptsDir \"OneDriveRemoval.ps1\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $oneDriveRemovalContent | Out-File -FilePath $oneDriveRemovalPath -Encoding UTF8 -Force");
        sb.AppendLine($"{indent}    Write-Log \"Created: OneDriveRemoval.ps1\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to create OneDriveRemoval.ps1: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendWinhanceInstallerScriptContent(StringBuilder sb, string indent = "")
    {
        sb.AppendLine($"{indent}# Create WinhanceInstall.ps1 script");
        sb.AppendLine($"{indent}$winhanceInstallContent = @'");
        sb.AppendLine(@"
function Get-FileFromWeb {
    param ([Parameter(Mandatory)][string]$URL, [Parameter(Mandatory)][string]$File)
    function Show-Progress {
        param ([Parameter(Mandatory)][Single]$TotalValue, [Parameter(Mandatory)][Single]$CurrentValue, [Parameter(Mandatory)][string]$ProgressText, [Parameter()][int]$BarSize = 10, [Parameter()][switch]$Complete)
        $percent = $CurrentValue / $TotalValue
        $percentComplete = $percent * 100
        if ($psISE) { Write-Progress ""$ProgressText"" -id 0 -percentComplete $percentComplete }
        else { Write-Host -NoNewLine ""`r$ProgressText $(''.PadRight($BarSize * $percent, [char]9608).PadRight($BarSize, [char]9617)) $($percentComplete.ToString('##0.00').PadLeft(6)) % "" }
    }
    try {
        $request = [System.Net.HttpWebRequest]::Create($URL)
        $response = $request.GetResponse()
        if ($response.StatusCode -eq 401 -or $response.StatusCode -eq 403 -or $response.StatusCode -eq 404) { throw ""Remote file either doesn't exist, is unauthorized, or is forbidden for '$URL'."" }
        if ($File -match '^\.\\') { $File = Join-Path (Get-Location -PSProvider 'FileSystem') ($File -Split '^\.')[1] }
        if ($File -and !(Split-Path $File)) { $File = Join-Path (Get-Location -PSProvider 'FileSystem') $File }
        if ($File) { $fileDirectory = $([System.IO.Path]::GetDirectoryName($File)); if (!(Test-Path($fileDirectory))) { [System.IO.Directory]::CreateDirectory($fileDirectory) | Out-Null } }
        [long]$fullSize = $response.ContentLength
        [byte[]]$buffer = new-object byte[] 1048576
        [long]$total = [long]$count = 0
        $reader = $response.GetResponseStream()
        $writer = new-object System.IO.FileStream $File, 'Create'
        do {
            $count = $reader.Read($buffer, 0, $buffer.Length)
            $writer.Write($buffer, 0, $count)
            $total += $count
            if ($fullSize -gt 0) { Show-Progress -TotalValue $fullSize -CurrentValue $total -ProgressText "" Downloading Winhance Installer"" }
        } while ($count -gt 0)
    }
    finally {
        $reader.Close()
        $writer.Close()
    }
}

$installerPath = ""C:\ProgramData\Winhance\Unattend\WinhanceInstaller.exe""
$downloadUrl = ""https://github.com/memstechtips/Winhance/releases/latest/download/Winhance.Installer.exe""

try {
    Write-Host ""Downloading Winhance Installer from GitHub..."" -ForegroundColor Cyan
    Get-FileFromWeb -URL $downloadUrl -File $installerPath
    Write-Host """"
    Write-Host ""Download completed successfully!"" -ForegroundColor Green
    Write-Host ""Launching Winhance Installer..."" -ForegroundColor Cyan
    Start-Process -FilePath $installerPath
    Write-Host ""Installer launched."" -ForegroundColor Green
} catch {
    Write-Host """"
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    Write-Host """"
    Write-Host ""Press any key to exit..."" -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}
");
        sb.AppendLine("'@");
        sb.AppendLine();
        sb.AppendLine($"{indent}$winhanceInstallPath = Join-Path $scriptsDir \"WinhanceInstall.ps1\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $winhanceInstallContent | Out-File -FilePath $winhanceInstallPath -Encoding UTF8 -Force");
        sb.AppendLine($"{indent}    Write-Log \"Created: WinhanceInstall.ps1\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to create WinhanceInstall.ps1: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}# Create desktop shortcut for Winhance installer");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $targetFile = Join-Path $scriptsDir \"WinhanceInstall.ps1\"");
        sb.AppendLine($"{indent}    $shortcutPath = \"C:\\Users\\Default\\Desktop\\Install Winhance.lnk\"");
        sb.AppendLine($"{indent}    $WshShell = New-Object -ComObject WScript.Shell");
        sb.AppendLine($"{indent}    $shortcut = $WshShell.CreateShortcut($shortcutPath)");
        sb.AppendLine($"{indent}    $shortcut.TargetPath = \"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"");
        sb.AppendLine($"{indent}    $shortcut.Arguments = \"-ExecutionPolicy Bypass -NoProfile -File `\"$targetFile`\"\"");
        sb.AppendLine($"{indent}    $shortcut.IconLocation = \"C:\\Windows\\System32\\appwiz.cpl,0\"");
        sb.AppendLine($"{indent}    $shortcut.WorkingDirectory = \"C:\\Windows\\System32\"");
        sb.AppendLine($"{indent}    $shortcut.Description = \"Launch Winhance Installer with Administrator Privileges\"");
        sb.AppendLine($"{indent}    $shortcut.Save()");
        sb.AppendLine($"{indent}    $bytes = [System.IO.File]::ReadAllBytes($shortcutPath)");
        sb.AppendLine($"{indent}    $bytes[21] = 34");
        sb.AppendLine($"{indent}    [System.IO.File]::WriteAllBytes($shortcutPath, $bytes)");
        sb.AppendLine($"{indent}    Write-Log \"Created desktop shortcut: $shortcutPath\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to create desktop shortcut: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private string GenerateBloatRemovalScriptContent(List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
    {
        var xboxPackages = new[] { "Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay" };
        var includeXboxFix = packages.Any(p => xboxPackages.Contains(p, StringComparer.OrdinalIgnoreCase));

        return BloatRemovalScriptGenerator.GenerateScript(packages, capabilities, optionalFeatures, specialApps, includeXboxFix);
    }


    private string SanitizeVariableName(string name)
    {
        return name.Replace("-", "_");
    }

private string? EscapePowerShellString(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace("'", "''");
    }

    private string ConvertRegistryPath(string registryPath)
    {
        return registryPath
            .Replace("HKEY_CURRENT_USER\\", "HKCU:\\")
            .Replace("HKEY_LOCAL_MACHINE\\", "HKLM:\\")
            .Replace("HKEY_CLASSES_ROOT\\", "HKCR:\\")
            .Replace("HKEY_USERS\\", "HKU:\\");
    }

    private string ConvertToRegistryType(RegistryValueKind valueType)
    {
        return valueType switch
        {
            RegistryValueKind.DWord => "DWord",
            RegistryValueKind.QWord => "QWord",
            RegistryValueKind.String => "String",
            RegistryValueKind.ExpandString => "ExpandString",
            RegistryValueKind.Binary => "Binary",
            RegistryValueKind.MultiString => "MultiString",
            _ => "String"
        };
    }

    private string FormatValueForPowerShell(object value, RegistryValueKind valueType)
    {
        if (value == null) return "$null";

        return valueType switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => $"'{value}'",
            RegistryValueKind.DWord or RegistryValueKind.QWord => value.ToString()!,
            RegistryValueKind.Binary when value is byte[] byteArray => $"@({string.Join(",", byteArray.Select(b => $"0x{b:X2}"))})",
            RegistryValueKind.Binary => $"@(0x{Convert.ToByte(value):X2})",
            _ => $"'{value}'"
        };
    }

    private void AppendWindowsUpdateDisabledModeLogic(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# WINDOWS UPDATE DISABLED MODE - ADDITIONAL HARDENING - Based on work by Chris Titus: https://github.com/ChrisTitusTech/winutil/blob/main/functions/public/Invoke-WPFUpdatesdisable.ps1");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Applying Windows Update Disabled mode hardening...\" \"INFO\"");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Disable Windows Update services");
        sb.AppendLine($"{indent}$updateServices = @('wuauserv', 'UsoSvc', 'WaaSMedicSvc')");
        sb.AppendLine($"{indent}foreach ($service in $updateServices) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        Write-Log \"Disabling service: $service\" \"INFO\"");
        sb.AppendLine($"{indent}        net stop $service 2>$null");
        sb.AppendLine($"{indent}        sc.exe config $service start= disabled 2>$null");
        sb.AppendLine($"{indent}        sc.exe failure $service reset= 0 actions= \"\" 2>$null");
        sb.AppendLine($"{indent}        Write-Log \"Disabled service: $service\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to disable $service : $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Disable Windows Update scheduled tasks");
        sb.AppendLine($"{indent}$taskPaths = @(");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\InstallService\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\UpdateOrchestrator\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\UpdateAssistant\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\WaaSMedic\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\WindowsUpdate\\*'");
        sb.AppendLine($"{indent})");
        sb.AppendLine();
        sb.AppendLine($"{indent}foreach ($taskPath in $taskPaths) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $tasks = Get-ScheduledTask -TaskPath $taskPath -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        foreach ($task in $tasks) {{");
        sb.AppendLine($"{indent}            try {{");
        sb.AppendLine($"{indent}                Disable-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -ErrorAction Stop | Out-Null");
        sb.AppendLine($"{indent}                Write-Log \"Disabled task: $($task.TaskPath)$($task.TaskName)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            }} catch {{");
        sb.AppendLine($"{indent}                Write-Log \"Skipped task: $($task.TaskPath)$($task.TaskName)\" \"WARNING\"");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to process tasks in $taskPath : $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Rename critical Windows Update DLLs");
        sb.AppendLine($"{indent}$updateDlls = @('WaaSMedicSvc.dll', 'wuaueng.dll')");
        sb.AppendLine($"{indent}foreach ($dll in $updateDlls) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $dllPath = \"C:\\Windows\\System32\\$dll\"");
        sb.AppendLine($"{indent}        $backupPath = \"C:\\Windows\\System32\\$($dll.Replace('.dll', '_BAK.dll'))\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}        if ((Test-Path $dllPath) -and -not (Test-Path $backupPath)) {{");
        sb.AppendLine($"{indent}            Write-Log \"Renaming $dll to backup\" \"INFO\"");
        sb.AppendLine($"{indent}            takeown /f \"$dllPath\" 2>$null | Out-Null");
        sb.AppendLine($"{indent}            icacls \"$dllPath\" /grant *S-1-1-0:F 2>$null | Out-Null");
        sb.AppendLine($"{indent}            Move-Item -Path $dllPath -Destination $backupPath -Force -ErrorAction Stop");
        sb.AppendLine($"{indent}            Write-Log \"Renamed $dll to backup\" \"SUCCESS\"");
        sb.AppendLine($"{indent}        }} elseif (Test-Path $backupPath) {{");
        sb.AppendLine($"{indent}            Write-Log \"$dll already backed up\" \"INFO\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to rename $dll : $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Cleanup SoftwareDistribution folder");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $softwareDistPath = 'C:\\Windows\\SoftwareDistribution'");
        sb.AppendLine($"{indent}    if (Test-Path $softwareDistPath) {{");
        sb.AppendLine($"{indent}        Write-Log \"Cleaning SoftwareDistribution folder...\" \"INFO\"");
        sb.AppendLine($"{indent}        Remove-Item \"$softwareDistPath\\*\" -Recurse -Force -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        Write-Log \"SoftwareDistribution folder cleaned\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to cleanup SoftwareDistribution: $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}Write-Log \"Windows Update Disabled mode hardening completed\" \"SUCCESS\"");
        sb.AppendLine();
    }

    private void AppendUserCustomizationsScheduledTask(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# USER CUSTOMIZATIONS SCHEDULED TASK");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Registering UserCustomizations scheduled task...\" \"INFO\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $action = New-ScheduledTaskAction -Execute \"powershell.exe\" -Argument \"-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File C:\\ProgramData\\Winhance\\Unattend\\Scripts\\Winhancements.ps1 -UserCustomizations\"");
        sb.AppendLine($"{indent}    $trigger = New-ScheduledTaskTrigger -AtLogOn");
        sb.AppendLine($"{indent}    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0");
        sb.AppendLine($"{indent}    $principal = New-ScheduledTaskPrincipal -UserId \"SYSTEM\" -LogonType ServiceAccount -RunLevel Highest");
        sb.AppendLine($"{indent}    Register-ScheduledTask -TaskName \"WinhanceUserCustomizations\" -TaskPath \"\\Winhance\" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null");
        sb.AppendLine($"{indent}    Write-Log \"Registered scheduled task: WinhanceUserCustomizations\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to register UserCustomizations task: `$(`$_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendCleanStartMenuSection(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# START MENU LAYOUT");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Configuring clean Start Menu layout...\" \"INFO\"");
        sb.AppendLine();

        sb.AppendLine($"{indent}$buildNumber = [System.Environment]::OSVersion.Version.Build");
        sb.AppendLine($"{indent}Write-Log \"Detected Windows build: $buildNumber\" \"INFO\"");
        sb.AppendLine();

        sb.AppendLine($"{indent}if ($buildNumber -ge 22000) {{");
        sb.AppendLine($"{indent}    Write-Log \"Applying Windows 11 clean Start Menu layout\" \"INFO\"");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        Set-RegistryValue -Path 'HKLM:\\SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Start' -Name 'ConfigureStartPins' -Type 'String' -Value '{{\"pinnedList\":[]}}' -Description 'Clean Start Menu'");
        sb.AppendLine($"{indent}        Write-Log \"Windows 11 Start Menu layout applied successfully\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to apply Windows 11 Start Menu layout: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");

        sb.AppendLine($"{indent}else {{");
        sb.AppendLine($"{indent}    Write-Log \"Applying Windows 10 clean Start Menu layout\" \"INFO\"");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        # Step 1: Create directory");
        sb.AppendLine($"{indent}        $ShellPath = \"C:\\Users\\Default\\AppData\\Local\\Microsoft\\Windows\\Shell\"");
        sb.AppendLine($"{indent}        New-Item -Path $ShellPath -ItemType Directory -Force | Out-Null");
        sb.AppendLine($"{indent}        Write-Log \"Created directory: $ShellPath\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}        # Step 2: Create XML content");
        sb.AppendLine($"{indent}        $xmlContent = @'");
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<LayoutModificationTemplate Version=\"1\" xmlns=\"http://schemas.microsoft.com/Start/2014/LayoutModification\">");
        sb.AppendLine("    <LayoutOptions StartTileGroupCellWidth=\"6\" />");
        sb.AppendLine("    <DefaultLayoutOverride>");
        sb.AppendLine("        <StartLayoutCollection>");
        sb.AppendLine("            <StartLayout GroupCellWidth=\"6\" xmlns=\"http://schemas.microsoft.com/Start/2014/FullDefaultLayout\" />");
        sb.AppendLine("        </StartLayoutCollection>");
        sb.AppendLine("    </DefaultLayoutOverride>");
        sb.AppendLine("</LayoutModificationTemplate>");
        sb.AppendLine("'@");
        sb.AppendLine();
        sb.AppendLine($"{indent}        # Step 3: Save XML file");
        sb.AppendLine($"{indent}        $XmlPath = \"$ShellPath\\LayoutModification.xml\"");
        sb.AppendLine($"{indent}        $xmlContent | Out-File -FilePath $XmlPath -Encoding UTF8");
        sb.AppendLine($"{indent}        Write-Log \"SUCCESS: Clean Start Menu Template created at $XmlPath\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to create Start Menu Template: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

/// <summary>
/// Emits registry entries for feature groups (Optimize/Customize), scheduled tasks,
/// wallpaper settings, and Windows Update disabled mode hardening.
/// </summary>
internal class FeatureRegistryScriptSection
{
    private readonly RegistryCommandEmitter _registryEmitter;
    private readonly ILogService _logService;

    public FeatureRegistryScriptSection(RegistryCommandEmitter registryEmitter, ILogService logService)
    {
        _registryEmitter = registryEmitter;
        _logService = logService;
    }

    public void AppendFeatureGroupRegistryEntries(
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

                if (settingDef.Id == SettingIds.PowerPlanSelection) continue;

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
                    _registryEmitter.AppendToggleCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
                }
                else if (configItem.InputType == InputType.Selection)
                {
                    _registryEmitter.AppendSelectionCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
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
                var updatePolicySetting = configSection.Items.FirstOrDefault(i => i.Id == SettingIds.UpdatesPolicyMode);
                if (updatePolicySetting?.SelectedIndex == 3)
                {
                    AppendWindowsUpdateDisabledModeLogic(sb, indent);
                }
            }
        }
    }

    public string GetFeatureDisplayName(string featureId)
    {
        var definition = FeatureDefinitions.Get(featureId);
        return definition != null ? $"{definition.DefaultName} Settings" : $"{featureId} Settings";
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
}

using System.Text;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

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
        IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> allSettings,
        bool isHkcu,
        string indent,
        WinBuild? build = null)
    {
        foreach (var featureKvp in featureGroup.Features)
        {
            var featureId = featureKvp.Key;
            var configSection = featureKvp.Value;

            if (!allSettings.TryGetValue(featureId, out var settings))
            {
                _logService.Log(LogLevel.Warning, $"Could not find Settings for feature: {featureId}");
                continue;
            }

            bool hasEntriesForCurrentHive = false;
            foreach (var configItem in configSection.Items)
            {
                // The dict carries the paired catalog Settings (keyed by canonical Id), so the per-item
                // pairing is a lookup by the alias-NORMALIZED config id - a config carrying a retired
                // "-win10" id still reaches its merged catalog Setting. A miss (unknown id) contributes no
                // presence - the established silent-skip.
                var setting = settings.FirstOrDefault(s => s.Id == SettingIdAliases.Normalize(configItem.Id));
                if (setting == null) continue;

                if (setting.Id == SettingIds.PowerPlanSelection) continue;

                // Presence gating reads the catalog Setting (Targets/Effects). It errs toward OVER-reporting
                // so a header is emitted whenever the emit could produce content (content is never dropped by
                // a skipped header).
                bool hasRegistry = AutounattendMechanismPresence.HasRegistryInHive(setting, isHkcu);
                bool hasTask = AutounattendMechanismPresence.HasScheduledTask(setting);
                bool hasScript = AutounattendMechanismPresence.HasScriptInHive(setting, isHkcu);

                if (hasRegistry || (!isHkcu && hasTask) || hasScript)
                    hasEntriesForCurrentHive = true;

                if (!isHkcu && setting.Id == "power-hibernation-enable")
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
                // Alias-normalized lookup of the paired catalog Setting in the dict (see the presence gate above).
                var setting = settings.FirstOrDefault(s => s.Id == SettingIdAliases.Normalize(configItem.Id));
                if (setting == null)
                {
                    _logService.Log(LogLevel.Warning, $"Could not find catalog Setting for: {configItem.Id}");
                    continue;
                }

                // Skip settings that are powercfg-backed with no registry writes (already handled in the Power
                // Settings section). Presence off the catalog Setting (PowerCfgTarget vs RegTarget/
                // RegistryWriteEffect).
                bool powerCfgOnly = AutounattendMechanismPresence.HasPowerCfg(setting)
                    && !AutounattendMechanismPresence.HasRegistry(setting);
                if (powerCfgOnly)
                    continue;

                // Set when an Action setting was emitted (registry + scripts) through the catalog Effects
                // emitter below, so the shared PowerShell-script block does not double-emit its scripts.
                bool actionHandledByCatalog = false;

                // Dispatch off the catalog Control.
                if (setting.Control == ControlKind.Toggle)
                {
                    // Every toggle routes through the catalog emitter (AppendToggleCommandsFromCatalog) with
                    // the live build threaded, so the OS-merged "This PC folder" toggles pick the
                    // build-appropriate target (Win11 HiddenByDefault vs Win10 KeyExists). The RegContents tail
                    // is emitted explicitly (off the active state's RegContentEffects) - a no-op for toggles
                    // without RegContent effects. A caller feeding NO build on a build-gated toggle gets the
                    // emitter's documented null-build behaviour (no target dropped); production always threads
                    // the live build.
                    _registryEmitter.AppendToggleCommandsFromCatalog(sb, setting, configItem, isHkcu, indent, build);
                    _registryEmitter.AppendRegContentCommandsFromCatalog(sb, setting, configItem.IsSelected, isHkcu, indent);
                }
                else if (setting.Control == ControlKind.Selection)
                {
                    _registryEmitter.AppendSelectionCommands(sb, setting, configItem, isHkcu, indent);
                }
                else if (setting.Control == ControlKind.Action)
                {
                    // Action settings are one-shot "apply" - only emit when the user actually selected them
                    // (AppendActionCommandsFromCatalog guards IsSelected internally; an unselected Action has
                    // no "disabled" semantic, so nothing may be emitted for it). Registry writes AND scripts
                    // both come from the setting-level catalog Effects. The shared script block below is skipped
                    // for this item so its scripts are not double-emitted.
                    AppendActionCommandsFromCatalog(sb, setting, configItem, isHkcu, indent);
                    actionHandledByCatalog = true;
                }

                // Emit PowerShell scripts whose RunContext matches the current pass. A Selection with NO
                // SelectedIndex (a "Custom" value matching no preset option) routes to the custom-state emitter
                // (AppendCustomStateScriptsFromCatalog, reading the un-baked Setting.CustomStateScripts). Every
                // other setting WITH states (toggle / selection-with-index) uses AppendPowerShellScriptsFromCatalog.
                // A setting with NO states (slider / power-plan; an Action set actionHandledByCatalog above) has
                // no state scripts, so nothing is emitted.
                if (!actionHandledByCatalog)
                {
                    bool selectionWithoutIndex = setting.Control == ControlKind.Selection && !configItem.SelectedIndex.HasValue;
                    if (selectionWithoutIndex)
                        AppendCustomStateScriptsFromCatalog(sb, setting, configItem, isHkcu, indent);
                    else if (setting.States.Count > 0)
                        AppendPowerShellScriptsFromCatalog(sb, setting, configItem, isHkcu, indent);
                }

            }

            if (!isHkcu)
            {
                var scheduledTasksToApply = new List<(string TaskName, string Action, string Description)>();

                foreach (var configItem in configSection.Items)
                {
                    // The scheduled-task paths + description come from the catalog Setting (TaskTarget +
                    // Display.Description), looked up in the dict by the alias-normalized id.
                    var setting = settings.FirstOrDefault(s => s.Id == SettingIdAliases.Normalize(configItem.Id));
                    if (setting != null)
                    {
                        scheduledTasksToApply.AddRange(CollectScheduledTasksFromCatalog(setting, configItem));
                    }

                    if (setting?.Id == "power-hibernation-enable")
                    {
                        var hibernateState = configItem.IsSelected == true ? "on" : "off";
                        sb.AppendLine();
                        sb.AppendLine($"{indent}Write-Log \"Setting hibernation to {hibernateState}...\" \"INFO\"");
                        sb.AppendLine($"{indent}powercfg /hibernate {hibernateState} 2>$null");
                        sb.AppendLine($"{indent}Write-Log \"Hibernation set to {hibernateState}\" \"SUCCESS\"");
                    }
                }

                if (scheduledTasksToApply.Count > 0)
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

    // Each option's preset ScriptVariables are already baked into ScriptEffect.Script, so only the runtime
    // CustomStateValues pass is re-applied here. LIMITATION: a Selection with a null SelectedIndex has no state to
    // resolve, so this emits nothing - the loop routes that shape to AppendCustomStateScriptsFromCatalog.
    internal void AppendPowerShellScriptsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        // Resolve the state whose ScriptEffects this pass should emit. A Selection keys off SelectedIndex; a
        // toggle/action keys off the Enabled/Disabled state (Custom values count as "enabled").
        SettingState? activeState;
        if (catalogSetting.Control == ControlKind.Selection
            && configItem.SelectedIndex.HasValue
            && configItem.SelectedIndex.Value >= 0
            && configItem.SelectedIndex.Value < catalogSetting.States.Count)
        {
            activeState = catalogSetting.States[configItem.SelectedIndex.Value];
        }
        else
        {
            var useEnabled = configItem.IsSelected == true || configItem.CustomStateValues?.Count > 0;
            var targetLabel = useEnabled ? "Enabled" : "Disabled";
            activeState = catalogSetting.States.FirstOrDefault(s => s.Label == targetLabel);
        }

        if (activeState is null)
        {
            return;
        }

        foreach (var scriptEffect in activeState.Effects.OfType<ScriptEffect>())
        {
            // User->HKCU / System->HKLM mapping.
            if ((scriptEffect.Run == RunContext.User) != isHkcu)
            {
                continue;
            }

            var script = scriptEffect.Script;

            // Runtime CustomStateValues substitution only. The option's preset ScriptVariables are already
            // baked into ScriptEffect.Script.
            if (!string.IsNullOrEmpty(script) && configItem.CustomStateValues is { } customValues)
            {
                foreach (var kvp in customValues)
                {
                    if (kvp.Value != null)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value.ToString() ?? string.Empty);
                    }
                }
            }

            if (!string.IsNullOrEmpty(script))
            {
                var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
                sb.AppendLine();
                sb.AppendLine($"{indent}# PowerShell script for: {catalogSetting.Display.Name}");
                sb.AppendLine($"{indent}try {{");
                foreach (var line in script.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        sb.AppendLine($"{indent}    {trimmedLine}");
                    }
                }
                sb.AppendLine($"{indent}    Write-Log \"{escapedDescription}\" \"SUCCESS\"");
                sb.AppendLine($"{indent}}} catch {{");
                sb.AppendLine($"{indent}    Write-Log \"Failed: {escapedDescription} - $($_.Exception.Message)\" \"ERROR\"");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }
    }

    // The "Custom" Selection shape (no SelectedIndex): emits from Setting.CustomStateScripts (the UN-BAKED scripts)
    // with only the runtime CustomStateValues placeholder pass; unmatched placeholders survive, since there is no
    // SelectedIndex and so no ScriptVariables source. DELIBERATE: with no CustomStateValues and IsSelected != true
    // this emits NOTHING rather than a reset script - such an item expresses no intent, and resetting e.g. the
    // user's DNS to automatic is the riskier behaviour.
    internal void AppendCustomStateScriptsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        // Custom state (user-entered values) always counts as "enabled" - the user picking Custom DNS is
        // expressing intent to configure, not to reset.
        bool useEnabled = configItem.CustomStateValues?.Count > 0 || configItem.IsSelected == true;

        // The deliberate no-intent behavior documented above: a no-intent shape emits nothing.
        if (!useEnabled)
        {
            return;
        }

        foreach (var scriptEffect in catalogSetting.CustomStateScripts)
        {
            // User->HKCU / System->HKLM mapping.
            if ((scriptEffect.Run == RunContext.User) != isHkcu)
            {
                continue;
            }

            var script = scriptEffect.Script;

            // Runtime CustomStateValues substitution - the ONLY placeholder source on this path (no SelectedIndex
            // means no preset option, so no ScriptVariables merge source exists): OrdinalIgnoreCase keys, null
            // values skipped, ToString() ?? "", literal {{key}} Replace; unmatched placeholders survive (the DNS
            // DoH script self-guards on a literal {{dohtemplate}}).
            if (!string.IsNullOrEmpty(script) && configItem.CustomStateValues is { } customValues)
            {
                var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in customValues)
                {
                    if (kvp.Value != null)
                    {
                        placeholders[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                    }
                }

                foreach (var kvp in placeholders)
                {
                    script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
                }
            }

            if (!string.IsNullOrEmpty(script))
            {
                var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
                sb.AppendLine();
                sb.AppendLine($"{indent}# PowerShell script for: {catalogSetting.Display.Name}");
                sb.AppendLine($"{indent}try {{");
                foreach (var line in script.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        sb.AppendLine($"{indent}    {trimmedLine}");
                    }
                }
                sb.AppendLine($"{indent}    Write-Log \"{escapedDescription}\" \"SUCCESS\"");
                sb.AppendLine($"{indent}}} catch {{");
                sb.AppendLine($"{indent}    Write-Log \"Failed: {escapedDescription} - $($_.Exception.Message)\" \"ERROR\"");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }
    }

    // ORDER: the registry pass runs before the script pass. Emits nothing unless the Action is selected.
    internal void AppendActionCommandsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        // Action one-shot: emit only when the user selected it (matches the Action branch's IsSelected == true guard).
        if (configItem.IsSelected != true)
            return;

        // Registry pass runs before the script pass.
        _registryEmitter.AppendActionRegistryCommandsFromCatalog(sb, catalogSetting, isHkcu, indent);

        // Script pass: setting-level ScriptEffects.
        AppendActionScriptsFromCatalog(sb, catalogSetting, configItem, isHkcu, indent);
    }

    // An Action has no options, so the only placeholder pass is the runtime CustomStateValues substitution.
    internal void AppendActionScriptsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        foreach (var scriptEffect in catalogSetting.Effects.OfType<ScriptEffect>())
        {
            // User->HKCU / System->HKLM mapping.
            if ((scriptEffect.Run == RunContext.User) != isHkcu)
                continue;

            var script = scriptEffect.Script;

            // Runtime CustomStateValues substitution only (an Action has no ComboBox-option ScriptVariables, so
            // that source is absent).
            if (!string.IsNullOrEmpty(script) && configItem.CustomStateValues is { } customValues)
            {
                foreach (var kvp in customValues)
                {
                    if (kvp.Value != null)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value.ToString() ?? string.Empty);
                    }
                }
            }

            if (!string.IsNullOrEmpty(script))
            {
                var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
                sb.AppendLine();
                sb.AppendLine($"{indent}# PowerShell script for: {catalogSetting.Display.Name}");
                sb.AppendLine($"{indent}try {{");
                foreach (var line in script.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        sb.AppendLine($"{indent}    {trimmedLine}");
                    }
                }
                sb.AppendLine($"{indent}    Write-Log \"{escapedDescription}\" \"SUCCESS\"");
                sb.AppendLine($"{indent}}} catch {{");
                sb.AppendLine($"{indent}    Write-Log \"Failed: {escapedDescription} - $($_.Exception.Message)\" \"ERROR\"");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }
    }

    internal IEnumerable<(string TaskName, string Action, string Description)> CollectScheduledTasksFromCatalog(
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting, ConfigurationItem configItem)
    {
        foreach (var taskTarget in catalogSetting.Targets.OfType<TaskTarget>())
        {
            var action = configItem.IsSelected == true ? "/Enable" : "/Disable";
            yield return (taskTarget.TaskPath, action, catalogSetting.Display.Description);
        }
    }

    internal void AppendScheduledTaskBatch(StringBuilder sb, List<(string TaskName, string Action, string Description)> tasks, string indent)
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

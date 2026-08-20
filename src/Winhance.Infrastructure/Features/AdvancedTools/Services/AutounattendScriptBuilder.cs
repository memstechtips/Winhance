using System.Text;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class AutounattendScriptBuilder : IAutounattendScriptBuilder
{
    private readonly ILogService _logService;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly AppRemovalScriptSection _appRemovalSection;

    public AutounattendScriptBuilder(
        ILogService logService,
        IPowerShellRunner powerShellRunner,
        IWindowsVersionService windowsVersionService)
    {
        _logService = logService;
        _powerShellRunner = powerShellRunner;
        _windowsVersionService = windowsVersionService;
        _appRemovalSection = new AppRemovalScriptSection();
    }

    public async Task<string> BuildAsync(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> byFeature)
    {
        WarnOnUnreachableNativePowerApiSettings(
            new HashSet<string>(set.Settings.Select(c => SettingIdAliases.Normalize(c.SettingId)), StringComparer.OrdinalIgnoreCase),
            byFeature);

        var currentBuild = new WinBuild(
            _windowsVersionService.GetWindowsBuildNumber(),
            _windowsVersionService.GetWindowsBuildRevision());

        var emitted = new ApplyOpScriptEmitter(_logService).Emit(set, byFeature, currentBuild, "    ", "            ");

        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHeader(sb);
        ScriptPreambleSection.AppendLoggingSetup(sb);
        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.AppendLine();
        sb.AppendLine("if (-not $UserCustomizations) {");
        sb.AppendLine();

        _appRemovalSection.AppendScriptsDirectorySetup(sb, "    ");

        if (set.WindowsApps.Count > 0)
        {
            await _appRemovalSection.AppendBloatRemovalScriptAsync(sb, set.WindowsApps, "    ").ConfigureAwait(false);
        }

        _appRemovalSection.AppendWinhanceInstallerScriptContent(sb, "    ");

        AppendPowerSection(sb, emitted.PowerPlan, emitted.PowerRows, "    ");

        foreach (var featureId in FeaturesInSectionOrder(byFeature))
        {
            if (!AppendFeaturePass(sb, featureId, emitted.SystemPassByFeature, "    "))
                continue;

            // Windows Update "Disabled" (option index 3) carries extra hardening the catalog cannot express.
            if (featureId == FeatureIds.Update
                && set.Settings.Any(c => c.SettingId == SettingIds.UpdatesPolicyMode && c.Value is ChoiceValue.Option { Index: 3 }))
            {
                AppendWindowsUpdateDisabledModeLogic(sb, "    ");
            }
        }

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "    ");

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        AppendCustomScriptPlaceholder(sb, "    ", "SYSTEM WIDE");

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("if ($UserCustomizations) {");
        sb.AppendLine();
        AppendUserDetectionBridge(sb);

        foreach (var featureId in FeaturesInSectionOrder(byFeature))
        {
            if (!AppendFeaturePass(sb, featureId, emitted.UserPassByFeature, "            "))
                continue;

            // The wallpaper is set whenever the theme feature is part of the file, not per setting.
            if (featureId == FeatureIds.WindowsTheme)
            {
                AppendWallpaperSetting(sb, "            ");
            }
        }

        AppendCustomScriptPlaceholder(sb, "            ", "USER SPECIFIC");

        AppendUserDetectionBridgeClosing(sb);

        ScriptPreambleSection.AppendCompletionBlock(sb);

        var scriptContent = sb.ToString();

        try
        {
            await _powerShellRunner.ValidateScriptSyntaxAsync(scriptContent).ConfigureAwait(false);
            _logService.Log(LogLevel.Info, "Winhancements.ps1 script passed PowerShell syntax validation");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Winhancements.ps1 script failed PowerShell syntax validation: {ex.Message}");
            throw;
        }

        return scriptContent;
    }

    // Optimize features first, then Customize, each in catalog order - the order the file has always been written in.
    private static IEnumerable<string> FeaturesInSectionOrder(IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> byFeature) =>
        byFeature.Keys.Where(FeatureDefinitions.OptimizeFeatures.Contains)
            .Concat(byFeature.Keys.Where(FeatureDefinitions.CustomizeFeatures.Contains));

    // A feature section is headed only when the pass has something to say for it.
    private static bool AppendFeaturePass(StringBuilder sb, string featureId, IReadOnlyDictionary<string, string> passByFeature, string indent)
    {
        if (!passByFeature.TryGetValue(featureId, out var text) || string.IsNullOrWhiteSpace(text))
            return false;

        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# {GetFeatureDisplayName(featureId).ToUpper()}");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.Append(text);
        return true;
    }

    // NativePowerApi settings are applied via a managed Win32 API at runtime and have no emitter in the autounattend
    // pipeline; a setting whose only payload is NativePowerApi would silently be skipped, so warn loudly.
    private void WarnOnUnreachableNativePowerApiSettings(
        HashSet<string> selectedIds,
        IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> allSettings)
    {
        foreach (var group in allSettings)
        {
            foreach (var setting in group.Value)
            {
                if (!selectedIds.Contains(setting.Id)) continue;

                // Native power is authored on only one setting (power-hibernation-enable).
                bool hasNativePower = AutounattendMechanismPresence.HasNativePower(setting);
                if (!hasNativePower) continue;

                bool hasAutounattendFallback =
                    AutounattendMechanismPresence.HasRegistry(setting)
                    || AutounattendMechanismPresence.HasPowerCfg(setting)
                    || AutounattendMechanismPresence.HasScript(setting)
                    || AutounattendMechanismPresence.HasRegContent(setting)
                    || AutounattendMechanismPresence.HasScheduledTask(setting)
                    || setting.Id == "power-hibernation-enable";

                if (!hasAutounattendFallback)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Setting '{setting.Id}' is applied only via NativePowerApiSettings, " +
                        $"which has no autounattend emitter. It will be silently skipped during " +
                        $"unattend install. Add a RegistrySettings or PowerCfgSettings fallback.");
                }
            }
        }
    }

    private static void AppendCustomScriptPlaceholder(StringBuilder sb, string indent, string scopeLabel)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# ADD YOUR {scopeLabel} POWERSHELL SCRIPT CONTENTS BELOW");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}# Start here");
        sb.AppendLine();
        sb.AppendLine($"{indent}# End here");
        sb.AppendLine();
    }

    private static void AppendUserDetectionBridge(StringBuilder sb)
    {
        sb.AppendLine("    $runningAsSystem = ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -eq 'S-1-5-18')");
        sb.AppendLine();
        sb.AppendLine("    if ($runningAsSystem) {");
        sb.AppendLine("        # ================================================================");
        sb.AppendLine("        # SYSTEM path: detect user, check marker, launch child as user");
        sb.AppendLine("        # ================================================================");
        sb.AppendLine("        Write-Log \"UserCustomizations running as SYSTEM, detecting logged-in user...\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine("        if (-not (Test-Path \"HKU:\\\")) {");
        sb.AppendLine("            New-PSDrive -PSProvider Registry -Name HKU -Root HKEY_USERS -ErrorAction SilentlyContinue | Out-Null");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        $targetUser = $null");
        sb.AppendLine("        for ($attempt = 1; $attempt -le 12; $attempt++) {");
        sb.AppendLine("            $targetUser = Get-TargetUser");
        sb.AppendLine("            if ($targetUser) { break }");
        sb.AppendLine("            Write-Log \"Waiting for user login (attempt $attempt/12)...\" \"INFO\"");
        sb.AppendLine("            Start-Sleep -Seconds 10");
        sb.AppendLine("        }");
        sb.AppendLine("        if (-not $targetUser) {");
        sb.AppendLine("            Write-Log \"No logged-in user detected after 2 minutes, will retry at next logon\" \"WARNING\"");
        sb.AppendLine("            exit 1");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        $targetUserSID = Get-UserSID -Username $targetUser");
        sb.AppendLine("        if (-not $targetUserSID) {");
        sb.AppendLine("            Write-Log \"Failed to get SID for user: $targetUser\" \"ERROR\"");
        sb.AppendLine("            exit 1");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        Write-Log \"Target user: $targetUser (SID: $targetUserSID)\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine("        # Check completion marker via HKU (no PSDrive remap needed)");
        sb.AppendLine("        $markerPath = \"HKU:\\$targetUserSID\\Software\\Winhance\"");
        sb.AppendLine("        $markerName = \"UserCustomizationsApplied\"");
        sb.AppendLine("        $alreadyApplied = $false");
        sb.AppendLine();
        sb.AppendLine("        try {");
        sb.AppendLine("            if (Test-Path $markerPath) {");
        sb.AppendLine("                $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue");
        sb.AppendLine("                if ($value.$markerName -eq 1) {");
        sb.AppendLine("                    $alreadyApplied = $true");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        } catch { }");
        sb.AppendLine();
        sb.AppendLine("        if ($alreadyApplied) {");
        sb.AppendLine("            Write-Log \"User customizations have already been applied for this user\" \"INFO\"");
        sb.AppendLine("            Write-Log \"To re-apply, delete: HKCU\\Software\\Winhance\\$markerName\" \"INFO\"");
        sb.AppendLine("            Write-Log \"No restart needed - customizations were already applied\" \"INFO\"");
        sb.AppendLine("        } else {");
        sb.AppendLine("            Write-Log \"Launching child process as interactive user to apply customizations...\" \"INFO\"");
        sb.AppendLine("            # Grant user write access to log file so child process can log");
        sb.AppendLine("            icacls $LogPath /grant \"${targetUser}:(M)\" 2>&1 | Out-Null");
        sb.AppendLine("            $scriptPath = $MyInvocation.MyCommand.Path");
        sb.AppendLine("            $cmdLine = \"powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File `\"$scriptPath`\" -UserCustomizations\"");
        sb.AppendLine("            $success = Start-ProcessAsUser -CommandLine $cmdLine");
        sb.AppendLine();
        sb.AppendLine("            if ($success) {");
        sb.AppendLine("                Write-Log \"Child process completed successfully\" \"SUCCESS\"");
        sb.AppendLine("                Write-Log \"Rebooting system to apply user customizations...\" \"INFO\"");
        sb.AppendLine("                # Wait 20 seconds to give the FirstLogon phase some more time before restarting");
        sb.AppendLine("                shutdown.exe /r /t 20");
        sb.AppendLine("            } else {");
        sb.AppendLine("                Write-Log \"Child process failed or timed out - will retry at next logon\" \"ERROR\"");
        sb.AppendLine("                exit 1");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    } else {");
        sb.AppendLine("        # ================================================================");
        sb.AppendLine("        # User path: apply HKCU entries (natural resolution, no remap)");
        sb.AppendLine("        # ================================================================");
        sb.AppendLine("        Write-Log \"UserCustomizations running as user\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine("        $markerPath = \"HKCU:\\Software\\Winhance\"");
        sb.AppendLine("        $markerName = \"UserCustomizationsApplied\"");
        sb.AppendLine("        $alreadyApplied = $false");
        sb.AppendLine();
        sb.AppendLine("        try {");
        sb.AppendLine("            if (Test-Path $markerPath) {");
        sb.AppendLine("                $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue");
        sb.AppendLine("                if ($value.$markerName -eq 1) {");
        sb.AppendLine("                    $alreadyApplied = $true");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        } catch { }");
        sb.AppendLine();
        sb.AppendLine("        if ($alreadyApplied) {");
        sb.AppendLine("            Write-Log \"User customizations have already been applied for this user\" \"INFO\"");
        sb.AppendLine("            Write-Log \"To re-apply, delete: $markerPath\\$markerName\" \"INFO\"");
        sb.AppendLine("        } else {");
        sb.AppendLine("            Write-Log \"Applying user customizations for the first time...\" \"INFO\"");
        sb.AppendLine();
    }

    private static void AppendUserDetectionBridgeClosing(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("            try {");
        sb.AppendLine("                if (-not (Test-Path $markerPath)) {");
        sb.AppendLine("                    New-Item -Path $markerPath -Force | Out-Null");
        sb.AppendLine("                }");
        sb.AppendLine("                Set-ItemProperty -Path $markerPath -Name $markerName -Value 1 -Type DWord -Force");
        sb.AppendLine("                Write-Log \"User customizations completed and marked as applied\" \"SUCCESS\"");
        sb.AppendLine("                Write-Log \"Note: User customizations will not run again unless $markerPath\\$markerName is deleted\" \"INFO\"");
        sb.AppendLine("            } catch {");
        sb.AppendLine("                Write-Log \"Failed to create completion marker: $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendPowerSection(StringBuilder sb, ChoiceValue.PowerPlan? plan, IReadOnlyList<PowerCfgRow> rows, string indent)
    {
        if (plan is null && rows.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# POWER PLAN & POWERCFG SETTINGS");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();

        if (plan is not null)
        {
            AppendPowerPlanCreation(sb, plan, indent);
        }

        if (rows.Count > 0)
        {
            AppendPowerSettingsApplication(sb, rows, plan?.Guid, indent);
        }
    }

    private static void AppendPowerPlanCreation(StringBuilder sb, ChoiceValue.PowerPlan plan, string indent)
    {
        var planGuid = plan.Guid;
        var planName = EscapeForDoubleQuotedString(plan.Name);

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

    private static void AppendPowerSettingsApplication(StringBuilder sb, IReadOnlyList<PowerCfgRow> powerSettings, string? powerPlanGuid, string indent)
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
            var escapedDescription = EscapeForDoubleQuotedString(setting.Description);
            var comma = i < powerSettings.Count - 1 ? "," : "";
            sb.AppendLine($"{indent}    @{{ S=\"{setting.SubgroupGuid}\"; G=\"{setting.SettingGuid}\"; AC={setting.Ac}; DC={setting.Dc}; N=\"{escapedDescription}\" }}{comma}");
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

    private static string GetFeatureDisplayName(string featureId)
    {
        var definition = FeatureDefinitions.Get(featureId);
        return definition != null ? $"{definition.DefaultName} Settings" : $"{featureId} Settings";
    }

    private static void AppendWallpaperSetting(StringBuilder sb, string indent)
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

    private static void AppendWindowsUpdateDisabledModeLogic(StringBuilder sb, string indent)
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

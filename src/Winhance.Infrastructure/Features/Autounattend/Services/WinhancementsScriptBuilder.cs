using System.Text;
using Winhance.Core.Features.Autounattend.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Infrastructure.Features.Autounattend.ScriptSections;
using static Winhance.Infrastructure.Features.Autounattend.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.Autounattend.Services;

internal sealed class WinhancementsScriptBuilder : IWinhancementsScriptBuilder
{
    private readonly ILogService _logService;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ILocalizationService _localizationService;
    private readonly IOptionProviderRegistry _optionProviders;
    private readonly AppRemovalScriptSection _appRemovalSection;

    public WinhancementsScriptBuilder(
        ILogService logService,
        IPowerShellRunner powerShellRunner,
        IWindowsVersionService windowsVersionService,
        ILocalizationService localizationService,
        IOptionProviderRegistry optionProviders)
    {
        _logService = logService;
        _powerShellRunner = powerShellRunner;
        _windowsVersionService = windowsVersionService;
        _localizationService = localizationService;
        _optionProviders = optionProviders;
        _appRemovalSection = new AppRemovalScriptSection();
    }

    public async Task<string> BuildAsync(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> byFeature, WinBuild? targetBuild = null)
    {
        // A version-specific registry target (This PC folders differ on Windows 10 and 11) resolves for the Windows
        // the file installs, not the PC making it.
        var build = targetBuild ?? new WinBuild(
            _windowsVersionService.GetWindowsBuildNumber(),
            _windowsVersionService.GetWindowsBuildRevision());

        // Removing Game Bar turns Game DVR off once, as removing it in the app does; the file applies it at setup.
        if (GameBarRemoval.Includes(set.WindowsApps.SelectMany(app => app.AppxPackageName ?? [])))
        {
            set = set with
            {
                Settings =
                [
                    .. set.Settings.Where(choice => choice.SettingId != GameBarRemoval.GameDvrSettingId),
                    new SettingChoice(GameBarRemoval.GameDvrSettingId, new ChoiceValue.Toggle(false)),
                ],
            };
        }

        var emitted = new ApplyOpScriptEmitter(_logService, _optionProviders, _localizationService).Emit(set, byFeature, build, "    ", "            ");
        var userScripts = UserScriptFiles.From(set);

        // The helper functions go above the body, but only the ones the body calls, so the body is built first.
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("if (-not $UserCustomizations) {");
        sb.AppendLine();

        // PowerShell maps no HKU: drive by default; the user bridge below maps its own.
        if (emitted.SystemPassByFeature.Values.Any(text => text.Contains(@"'HKU:\", StringComparison.Ordinal)))
        {
            sb.AppendLine("    New-PSDrive -PSProvider Registry -Name HKU -Root HKEY_USERS -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine();
        }

        if (set.WindowsApps.Count > 0)
        {
            _appRemovalSection.AppendScriptsDirectorySetup(sb, "    ");
            await _appRemovalSection.AppendBloatRemovalScriptAsync(sb, set.WindowsApps, "    ").ConfigureAwait(false);
        }

        AppendPowerSection(sb, emitted.PowerPlan, emitted.PowerRows, HiddenRows(emitted.PowerRows, byFeature), "    ");

        foreach (var featureId in FeaturesInSectionOrder(byFeature))
        {
            if (!AppendFeaturePass(sb, featureId, emitted.SystemPassByFeature, "    "))
                continue;

            // Windows Update "Disabled" (option index 3) carries extra hardening the catalog cannot express.
            if (featureId == FeatureIds.Update
                && set.Settings.Any(c => c.SettingId == "updates-policy-mode" && c.Value is ChoiceValue.Option { Index: 3 }))
            {
                AppendWindowsUpdateDisabledModeLogic(sb, "    ");
            }
        }

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        AppendCustomScriptPlaceholder(sb, "    ", "SYSTEM WIDE", userScripts.Where(s => s.Run == RunContext.System));

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("if ($UserCustomizations) {");
        sb.AppendLine();
        AppendUserDetectionBridge(sb, emitted.SignInAfterSetup);

        foreach (var featureId in FeaturesInSectionOrder(byFeature))
        {
            AppendFeaturePass(sb, featureId, emitted.UserPassByFeature, "            ");
        }

        AppendCustomScriptPlaceholder(sb, "            ", "USER SPECIFIC", userScripts.Where(s => s.Run == RunContext.User));

        AppendUserDetectionBridgeClosing(sb);

        ScriptPreambleSection.AppendCompletionBlock(sb);

        var body = sb.ToString();
        var script = new StringBuilder();
        ScriptPreambleSection.AppendHeader(script);
        ScriptPreambleSection.AppendLoggingSetup(script);
        // A .ps1 of the user's runs in this session, so a helper it calls goes into the file too.
        ScriptPreambleSection.AppendHelperFunctions(script,
            string.Join(Environment.NewLine, userScripts.Where(s => s.Extension == ".ps1").Select(s => s.Content).Prepend(body)));
        var scriptContent = script.Append(body).ToString();

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

    private static IEnumerable<string> FeaturesInSectionOrder(IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> byFeature) =>
        byFeature.Keys.Where(FeatureDefinitions.OptimizeFeatures.Contains)
            .Concat(byFeature.Keys.Where(FeatureDefinitions.CustomizeFeatures.Contains))
            .Concat(byFeature.Keys.Where(FeatureDefinitions.AutounattendFeatures.Contains));

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

    private static void AppendCustomScriptPlaceholder(StringBuilder sb, string indent, string scopeLabel, IEnumerable<UserScriptFile> userScripts)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# ADD YOUR {scopeLabel} POWERSHELL SCRIPT CONTENTS BELOW");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}# Start here");
        sb.AppendLine();
        foreach (var userScript in userScripts)
            AppendUserScript(sb, indent, userScript);
        sb.AppendLine($"{indent}# End here");
        sb.AppendLine();
    }

    // A .ps1 runs in this session so it can call the helpers above; the rest run in their own process and report
    // through the exit code. Each sits in try/catch so a broken one cannot stop the rest of the file.
    private static void AppendUserScript(StringBuilder sb, string indent, UserScriptFile userScript)
    {
        var name = userScript.Name;
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    Write-Log \"Running your own script {name}\" \"INFO\"");
        if (userScript.Extension == ".ps1")
        {
            sb.AppendLine($"{indent}    & \"{userScript.Path}\"");
            sb.AppendLine($"{indent}    Write-Log \"Your own script {name} finished\" \"SUCCESS\"");
        }
        else
        {
            var command = userScript.Extension == ".reg" ? "reg.exe import" : "cmd.exe /c";
            sb.AppendLine($"{indent}    & {command} \"{userScript.Path}\" 2>&1 | ForEach-Object {{ Write-Log \"$_\" \"INFO\" }}");
            sb.AppendLine($"{indent}    if ($LASTEXITCODE -eq 0) {{");
            sb.AppendLine($"{indent}        Write-Log \"Your own script {name} finished\" \"SUCCESS\"");
            sb.AppendLine($"{indent}    }} else {{");
            sb.AppendLine($"{indent}        Write-Log \"Your own script {name} exited with code $LASTEXITCODE\" \"WARNING\"");
            sb.AppendLine($"{indent}    }}");
        }
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Your own script {name} failed: $_\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static void AppendUserDetectionBridge(StringBuilder sb, string signInAfterSetup)
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
        sb.Append(signInAfterSetup);
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

    // Windows hides some power settings until their Attributes value is cleared; the catalog gives those targets an
    // EnablementKey.
    private static List<PowerCfgRow> HiddenRows(IReadOnlyList<PowerCfgRow> rows, IReadOnlyDictionary<string, IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>> byFeature)
    {
        var hidden = byFeature.Values
            .SelectMany(settings => settings)
            .SelectMany(setting => setting.Targets.OfType<PowerCfgTarget>())
            .Where(target => target.EnablementKey is not null)
            .Select(target => (target.SubgroupGuid, target.SettingGuid))
            .ToHashSet();
        return rows.Where(row => hidden.Contains((row.SubgroupGuid, row.SettingGuid))).ToList();
    }

    private static void AppendPowerSection(StringBuilder sb, PowerPlanChoice? plan, IReadOnlyList<PowerCfgRow> rows, IReadOnlyList<PowerCfgRow> hiddenRows, string indent)
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
            AppendPowerSettingsApplication(sb, rows, hiddenRows, plan is not null, indent);
        }

        if (plan is not null)
        {
            AppendPowerPlanActivation(sb, indent);
        }
    }

    private static void AppendPowerPlanCreation(StringBuilder sb, PowerPlanChoice plan, string indent)
    {
        var planGuid = plan.Guid;
        var planName = EscapeForDoubleQuotedString(plan.Name);

        sb.AppendLine($"{indent}Write-Log \"Setting up power plan: {planName}...\" \"INFO\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}$customPlanGuid = \"{planGuid}\"");
        sb.AppendLine();
        // powercfg /query also answers for a hidden template (Ultimate Performance's GUID on a fresh install), which
        // cannot be activated and blocks a copy under its GUID, so existence is read from /list and Windows picks the
        // copy's GUID when the requested one is taken.
        sb.AppendLine($"{indent}if (powercfg /list | Select-String -SimpleMatch $customPlanGuid) {{");
        sb.AppendLine($"{indent}    Write-Log \"Power plan already exists, using existing plan\" \"INFO\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    Write-Log \"Creating new power plan from Balanced...\" \"INFO\"");
        sb.AppendLine($"{indent}    $result = powercfg /duplicatescheme {PowerPlanCatalog.BalancedGuid} $customPlanGuid 2>&1");
        sb.AppendLine($"{indent}    if ($LASTEXITCODE -ne 0) {{");
        sb.AppendLine($"{indent}        $result = powercfg /duplicatescheme {PowerPlanCatalog.BalancedGuid} 2>&1");
        sb.AppendLine($"{indent}        if ($LASTEXITCODE -eq 0 -and \"$result\" -match '[0-9a-fA-F]{{8}}(-[0-9a-fA-F]{{4}}){{3}}-[0-9a-fA-F]{{12}}') {{");
        sb.AppendLine($"{indent}            $customPlanGuid = $Matches[0]");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}    if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}        powercfg /changename $customPlanGuid \"{planName}\" | Out-Null");
        sb.AppendLine($"{indent}        Write-Log \"Power plan created\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} else {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to create power plan: $result\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static void AppendPowerSettingsApplication(StringBuilder sb, IReadOnlyList<PowerCfgRow> powerSettings, IReadOnlyList<PowerCfgRow> hiddenSettings, bool hasPlan, string indent)
    {
        if (hiddenSettings.Count > 0)
        {
            sb.AppendLine($"{indent}Write-Log \"Enabling hidden power settings...\" \"INFO\"");
            sb.AppendLine($"{indent}$PowerSettingsBasePath = \"HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerSettings\"");
            sb.AppendLine($"{indent}$hiddenSettings = @(");
            for (int i = 0; i < hiddenSettings.Count; i++)
            {
                var hidden = hiddenSettings[i];
                var comma = i < hiddenSettings.Count - 1 ? "," : "";
                sb.AppendLine($"{indent}    @{{ Subgroup = \"{hidden.SubgroupGuid}\"; Setting = \"{hidden.SettingGuid}\" }}{comma}");
            }
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
        }

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

        var targetGuid = hasPlan ? "$customPlanGuid" : "\"SCHEME_CURRENT\"";
        sb.AppendLine($"{indent}$appliedCount = 0");
        sb.AppendLine($"{indent}$targetPlanGuid = {targetGuid}");
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
    }

    private static void AppendPowerPlanActivation(StringBuilder sb, string indent)
    {
        sb.AppendLine($"{indent}Write-Log \"Activating power plan...\" \"INFO\"");
        sb.AppendLine($"{indent}powercfg /setactive $customPlanGuid 2>$null");
        sb.AppendLine($"{indent}if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}    Write-Log \"Power plan activated successfully\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to activate power plan\" \"WARNING\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static string GetFeatureDisplayName(string featureId)
    {
        var definition = FeatureDefinitions.Get(featureId);
        return definition != null ? $"{definition.DefaultName} Settings" : $"{featureId} Settings";
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

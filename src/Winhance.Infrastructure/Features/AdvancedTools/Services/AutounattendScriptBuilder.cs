using System.Linq;
using System.Text;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

public class AutounattendScriptBuilder
{
    private readonly ILogService _logService;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly FeatureRegistryScriptSection _featureRegistrySection;
    private readonly PowerSettingsScriptSection _powerSettingsSection;
    private readonly AppRemovalScriptSection _appRemovalSection;

    public AutounattendScriptBuilder(
        IPowerSettingsQueryService powerSettingsQueryService,
        IHardwareDetectionService hardwareDetectionService,
        ILogService logService,
        IComboBoxResolver comboBoxResolver,
        IPowerShellRunner powerShellRunner)
    {
        _logService = logService;
        _powerShellRunner = powerShellRunner;

        var registryEmitter = new RegistryCommandEmitter(comboBoxResolver, logService);
        _featureRegistrySection = new FeatureRegistryScriptSection(registryEmitter, logService);
        _powerSettingsSection = new PowerSettingsScriptSection(powerSettingsQueryService, hardwareDetectionService, logService);
        _appRemovalSection = new AppRemovalScriptSection();
    }

    public async Task<string> BuildWinhancementsScriptAsync(
        UnifiedConfigurationFile config,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        var sb = new StringBuilder();

        // 1. Header and setup
        ScriptPreambleSection.AppendHeader(sb);
        ScriptPreambleSection.AppendLoggingSetup(sb);
        ScriptPreambleSection.AppendHelperFunctions(sb);

        // 2. Build if (-not $UserCustomizations) block
        sb.AppendLine();
        sb.AppendLine("if (-not $UserCustomizations) {");
        sb.AppendLine();

        _appRemovalSection.AppendScriptsDirectorySetup(sb, "    ");

        if (config.WindowsApps.Items.Any())
        {
            await _appRemovalSection.AppendBloatRemovalScriptAsync(sb, config.WindowsApps.Items, "    ").ConfigureAwait(false);
        }

        _appRemovalSection.AppendWinhanceInstallerScriptContent(sb, "    ");

        // 2b. Power settings
        await _powerSettingsSection.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ").ConfigureAwait(false);

        // 2c. HKLM registry entries from Optimize
        if (config.Optimize.Features.Any())
        {
            _featureRegistrySection.AppendFeatureGroupRegistryEntries(sb, config.Optimize, allSettings, "Optimize", isHkcu: false, indent: "    ");
        }

        // 2d. HKLM registry entries from Customize
        if (config.Customize.Features.Any())
        {
            _featureRegistrySection.AppendFeatureGroupRegistryEntries(sb, config.Customize, allSettings, "Customize", isHkcu: false, indent: "    ");
        }

        // 2e. Clean Start Menu Layout (always included)
        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "    ");

        // 2f. Register UserCustomizations scheduled task
        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        // 2g. System-wide custom script placeholder
        AppendCustomScriptPlaceholder(sb, "    ", "SYSTEM WIDE");

        sb.AppendLine("}");
        sb.AppendLine();

        // 3. Build if ($UserCustomizations) block
        sb.AppendLine("if ($UserCustomizations) {");
        sb.AppendLine();
        AppendUserDetectionBridge(sb);

        // 3a. HKCU registry entries from Optimize
        if (config.Optimize.Features.Any())
        {
            _featureRegistrySection.AppendFeatureGroupRegistryEntries(sb, config.Optimize, allSettings, "Optimize", isHkcu: true, indent: "            ");
        }

        // 3b. HKCU registry entries from Customize
        if (config.Customize.Features.Any())
        {
            _featureRegistrySection.AppendFeatureGroupRegistryEntries(sb, config.Customize, allSettings, "Customize", isHkcu: true, indent: "            ");
        }

        // 3c. User-specific custom script placeholder
        AppendCustomScriptPlaceholder(sb, "            ", "USER SPECIFIC");

        AppendUserDetectionBridgeClosing(sb);

        // 4. Completion block
        ScriptPreambleSection.AppendCompletionBlock(sb);

        var scriptContent = sb.ToString();

        // Validate the generated script has no PowerShell syntax errors
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

    /// <summary>
    /// Emits the SYSTEM/User detection bridge at the start of the $UserCustomizations block.
    /// This is the ~130-line inline block that detects the logged-in user, checks
    /// a completion marker, and launches a child process as the interactive user.
    /// </summary>
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

    /// <summary>
    /// Emits the closing braces and completion marker for the $UserCustomizations block.
    /// </summary>
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
}

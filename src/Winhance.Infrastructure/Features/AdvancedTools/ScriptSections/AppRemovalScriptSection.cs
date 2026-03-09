using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

/// <summary>
/// Handles scripts directory setup, app removal script embedding, and the Winhance installer script.
/// </summary>
internal class AppRemovalScriptSection
{
    public void AppendScriptsDirectorySetup(StringBuilder sb, string indent = "")
    {
        sb.AppendLine($"{indent}$scriptsDir = \"{ScriptPaths.ScriptsDirectoryLiteral}\"");
        sb.AppendLine($"{indent}if (!(Test-Path $scriptsDir)) {{");
        sb.AppendLine($"{indent}    New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null");
        sb.AppendLine($"{indent}    Write-Log \"Created scripts directory: $scriptsDir\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    Write-Log \"Scripts directory already exists: $scriptsDir\" \"INFO\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    public async Task AppendBloatRemovalScriptAsync(StringBuilder sb, IReadOnlyList<ConfigurationItem> selectedApps, string indent = "")
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
            else if (app.AppxPackageName?.Length > 0)
            {
                regularApps.AddRange(app.AppxPackageName);

                if (app.AppxPackageName.Any(name => name.Contains("OneNote", StringComparison.OrdinalIgnoreCase)) &&
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
            AppendEmbeddedScript(sb, "BloatRemoval", "bloatRemoval",
                GenerateBloatRemovalScriptContent(regularApps, capabilities, optionalFeatures, specialApps), indent);
        }

        // Embed EdgeRemoval.ps1 if needed
        if (edgeRemovalNeeded)
        {
            AppendEmbeddedScript(sb, "EdgeRemoval", "edgeRemoval", EdgeRemovalScript.GetScript(), indent);
        }

        // Embed OneDriveRemoval.ps1 if needed
        if (oneDriveRemovalNeeded)
        {
            AppendEmbeddedScript(sb, "OneDriveRemoval", "oneDriveRemoval", OneDriveRemovalScript.GetScript(), indent);
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

    /// <summary>
    /// Unified helper that replaces the three structurally identical AppendXxxScriptContent methods.
    /// </summary>
    private void AppendEmbeddedScript(StringBuilder sb, string scriptName, string varPrefix, string scriptContent, string indent)
    {
        sb.AppendLine($"{indent}# Create {scriptName}.ps1 script");
        sb.AppendLine($"{indent}${varPrefix}Content = @'");
        sb.Append(scriptContent);
        sb.AppendLine("'@");
        sb.AppendLine();
        sb.AppendLine($"{indent}${varPrefix}Path = Join-Path $scriptsDir \"{scriptName}.ps1\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    ${varPrefix}Content | Out-File -FilePath ${varPrefix}Path -Encoding UTF8 -Force");
        sb.AppendLine($"{indent}    Write-Log \"Created: {scriptName}.ps1\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to create {scriptName}.ps1: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    public void AppendWinhanceInstallerScriptContent(StringBuilder sb, string indent = "")
    {
        sb.AppendLine($"{indent}# Create desktop shortcut for Winhance installer");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $shortcutPath = \"C:\\Users\\Default\\Desktop\\Install Winhance.lnk\"");
        sb.AppendLine($"{indent}    $WshShell = New-Object -ComObject WScript.Shell");
        sb.AppendLine($"{indent}    $shortcut = $WshShell.CreateShortcut($shortcutPath)");
        sb.AppendLine($"{indent}    $shortcut.TargetPath = \"{ScriptPaths.PowerShellExePath}\"");
        sb.AppendLine($"{indent}    $shortcut.Arguments = \"-ExecutionPolicy Bypass -NoProfile -Command `\"irm 'https://get.winhance.net' | iex`\"\"");
        sb.AppendLine($"{indent}    $shortcut.IconLocation = \"C:\\Windows\\System32\\appwiz.cpl,0\"");
        sb.AppendLine($"{indent}    $shortcut.WorkingDirectory = \"C:\\Windows\\System32\"");
        sb.AppendLine($"{indent}    $shortcut.Description = \"Download and install Winhance from GitHub\"");
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
}

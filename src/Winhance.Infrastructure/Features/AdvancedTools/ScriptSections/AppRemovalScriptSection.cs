using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
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
        sb.AppendLine($"{indent}$scriptsDir = \"C:\\ProgramData\\Winhance\\Scripts\"");
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
}

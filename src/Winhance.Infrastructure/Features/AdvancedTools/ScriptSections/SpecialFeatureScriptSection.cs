using System.Text;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

/// <summary>
/// Handles special feature script sections: user customizations scheduled task and clean start menu layout.
/// </summary>
internal static class SpecialFeatureScriptSection
{
    public static void AppendUserCustomizationsScheduledTask(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# USER CUSTOMIZATIONS SCHEDULED TASK");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Registering UserCustomizations scheduled task...\" \"INFO\"");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $action = New-ScheduledTaskAction -Execute \"powershell.exe\" -Argument \"-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File {ScriptPaths.UnattendScriptPath} -UserCustomizations\"");
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

    public static void AppendCleanStartMenuSection(StringBuilder sb, string indent)
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

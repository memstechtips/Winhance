using System.Text;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Infrastructure.Features.Autounattend.ScriptSections;

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
        sb.AppendLine($"{indent}    $action = New-ScheduledTaskAction -Execute \"powershell.exe\" -Argument \"-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File {ScriptPaths.AutounattendScriptPath} -UserCustomizations\"");
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
}

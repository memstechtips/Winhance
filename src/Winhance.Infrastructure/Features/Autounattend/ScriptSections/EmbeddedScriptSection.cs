using System.Text;

namespace Winhance.Infrastructure.Features.Autounattend.ScriptSections;

// Winhancements.ps1 writes a script into $scriptsDir and, for the one-time kind, registers the task that runs it.
internal static class EmbeddedScriptSection
{
    internal static void AppendEmbeddedScript(StringBuilder sb, string scriptName, string scriptContent, string indent)
    {
        var varPrefix = VariableNameOf(scriptName);
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

    internal static string VariableNameOf(string scriptName) => char.ToLowerInvariant(scriptName[0]) + scriptName[1..];

    // One AtStartup task as SYSTEM, to be registered after the script it runs is written to $scriptsDir.
    internal static void AppendRegisterStartupTask(StringBuilder sb, string taskName, string indent)
    {
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $action = New-ScheduledTaskAction -Execute \"powershell.exe\" -Argument \"-ExecutionPolicy Bypass -NoProfile -File `\"$scriptsDir\\{taskName}.ps1`\"\"");
        sb.AppendLine($"{indent}    $trigger = New-ScheduledTaskTrigger -AtStartup");
        sb.AppendLine($"{indent}    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0");
        sb.AppendLine($"{indent}    $principal = New-ScheduledTaskPrincipal -UserId \"SYSTEM\" -LogonType ServiceAccount -RunLevel Highest");
        sb.AppendLine($"{indent}    Register-ScheduledTask -TaskName \"{taskName}\" -TaskPath \"\\Winhance\" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null");
        sb.AppendLine($"{indent}    Write-Log \"Registered scheduled task: {taskName}\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to register task {taskName}: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
    }
}

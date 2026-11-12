using System.Text;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.Common.Utilities;
using static Winhance.Infrastructure.Features.Autounattend.ScriptSections.EmbeddedScriptSection;

namespace Winhance.Infrastructure.Features.Autounattend.ScriptSections;

internal class AppRemovalScriptSection
{
    internal const string AfterUpdateName = "WinhanceRemoveAppsAfterUpdate";

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

    public async Task AppendBloatRemovalScriptAsync(StringBuilder sb, IReadOnlyList<AppChoice> selectedApps, string indent = "", AppRemovalMode mode = AppRemovalMode.Scripts)
    {
        var plan = AppRemovalPlan.For(selectedApps);
        var hasBloatWork = plan.Packages.Count > 0 || plan.Capabilities.Count > 0 || plan.Features.Count > 0 || plan.SpecialApps.Count > 0;

        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# WINDOWS APPS REMOVAL");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();

        if (hasBloatWork)
        {
            AppendEmbeddedScript(sb, BloatRemovalScriptGenerator.ScriptName, GenerateBloatRemovalScriptContent(plan), indent);
        }

        foreach (var item in plan.Dedicated)
        {
            var name = item.RemovalScriptName!;
            AppendEmbeddedScript(sb, name, item.RemovalScript!(), indent);
        }

        sb.AppendLine();
        sb.AppendLine(mode == AppRemovalMode.Scripts
            ? $"{indent}# Execute removal scripts and register scheduled tasks"
            : $"{indent}# Execute removal scripts");
        sb.AppendLine($"{indent}$scriptsToExecute = @()");
        var scriptNames = new List<string>();

        if (hasBloatWork)
        {
            // The image build removes packages, capabilities and features; a registry-uninstall app such as OneNote
            // only goes on the installed system.
            var bloatAfterImage = plan.SpecialApps.Count > 0 ? "; AfterImage = $true" : "";
            scriptNames.Add(BloatRemovalScriptGenerator.ScriptName);
            sb.AppendLine($"{indent}$scriptsToExecute += @{{Path = \"$scriptsDir\\{BloatRemovalScriptGenerator.ScriptName}.ps1\"; Name = \"{BloatRemovalScriptGenerator.ScriptName}\"; TriggerType = \"Logon\"{bloatAfterImage}}}");
        }

        foreach (var item in plan.Dedicated)
        {
            var name = item.RemovalScriptName!;
            var trigger = item.RemovalScriptRunsAtStartup ? "Startup" : "Logon";
            // The image build deletes Edge's files only, and OneDrive's installed-system cleanup (its setup, scheduled
            // tasks, shell folders) has no offline form; both scripts still run after the image.
            var afterImage = name is EdgeRemovalScript.ScriptName or OneDriveRemovalScript.ScriptName ? "; AfterImage = $true" : "";
            scriptNames.Add(name);
            sb.AppendLine($"{indent}$scriptsToExecute += @{{Path = \"$scriptsDir\\{name}.ps1\"; Name = \"{name}\"; TriggerType = \"{trigger}\"{afterImage}}}");
        }

        sb.AppendLine();
        sb.AppendLine($"{indent}$appsRemovedInImage = Test-Path '{ScriptPaths.UnattendAppsRemovedMarker}'");
        sb.AppendLine($"{indent}if ($appsRemovedInImage) {{");
        sb.AppendLine($"{indent}    Write-Log \"Windows apps were already removed in the image, running only the clean-up the image could not do\" \"INFO\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}foreach ($script in $scriptsToExecute) {{");
        sb.AppendLine($"{indent}    if (Test-Path $script.Path) {{");
        sb.AppendLine($"{indent}        if (-not $appsRemovedInImage -or $script.AfterImage) {{");
        sb.AppendLine($"{indent}            Write-Log \"Executing $($script.Name) script...\" \"INFO\"");
        sb.AppendLine($"{indent}            try {{");
        sb.AppendLine($"{indent}                Start-Process powershell.exe -ArgumentList \"-ExecutionPolicy Bypass -NoProfile -File `\"$($script.Path)`\"\" -Wait -NoNewWindow");
        sb.AppendLine($"{indent}                Write-Log \"$($script.Name) execution completed\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            }} catch {{");
        sb.AppendLine($"{indent}                Write-Log \"$($script.Name) execution failed: $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}        }}");

        if (mode == AppRemovalMode.Scripts)
            AppendTaskRegistration(sb, indent);

        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        if (mode == AppRemovalMode.Once)
            AppendRemoveOnceCleanup(sb, scriptNames, indent);
        else if (mode == AppRemovalMode.Winhance)
            AppendKeptList(sb, selectedApps, indent);

        sb.AppendLine($"{indent}Write-Log \"Windows Apps removal configuration completed\" \"SUCCESS\"");
    }

    private static void AppendTaskRegistration(StringBuilder sb, string indent)
    {
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
    }

    // The apps stay removed on their own if no update is waiting; otherwise one startup task removes them again once
    // the update has installed, and deletes itself and the scripts when it is done.
    private void AppendRemoveOnceCleanup(StringBuilder sb, IReadOnlyList<string> scriptNames, string indent)
    {
        var scriptPaths = string.Join(", ", scriptNames.Select(name => $"'{ScriptPaths.ScriptsDirectoryLiteral}\\{name}.ps1'"));

        sb.AppendLine($"{indent}if (Test-Path '{ScriptPaths.UnattendUpdatesDirectory}') {{");
        sb.AppendLine($"{indent}    Write-Log \"A Windows update is staged, registering a one-time task to remove the apps again after it installs\" \"INFO\"");
        AppendEmbeddedScript(sb, AfterUpdateName, AfterUpdateScriptContent(scriptPaths), indent + "    ");
        AppendRegisterStartupTask(sb, AfterUpdateName, indent + "    ");
        sb.AppendLine($"{indent}}} else {{");
        AppendDeleteScripts(sb, indent + "    ");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static void AppendKeptList(StringBuilder sb, IReadOnlyList<AppChoice> selectedApps, string indent)
    {
        var ids = string.Join(", ", selectedApps.Select(app => app.Id).Distinct().Select(id => $"'{id}'"));

        sb.AppendLine($"{indent}# Winhance keeps these removed");
        sb.AppendLine($"{indent}$keptKey = 'Registry::{KeepRemovedModeStore.Key}'");
        sb.AppendLine($"{indent}New-Item -Path $keptKey -Force | Out-Null");
        sb.AppendLine($"{indent}Set-ItemProperty -Path $keptKey -Name '{KeepRemovedModeStore.ModeValue}' -Value '{KeepRemovedMode.Winhance}' -Type String");
        sb.AppendLine($"{indent}Set-ItemProperty -Path $keptKey -Name '{KeepRemovedModeStore.EnabledValue}' -Value 1 -Type DWord");
        sb.AppendLine($"{indent}foreach ($appId in @({ids})) {{");
        sb.AppendLine($"{indent}    Set-ItemProperty -Path $keptKey -Name $appId -Value (Get-Date).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) -Type String");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Recorded the removed apps for Winhance to keep removed\" \"SUCCESS\"");
        AppendDeleteScripts(sb, indent);
        sb.AppendLine();
    }

    private static void AppendDeleteScripts(StringBuilder sb, string indent)
    {
        sb.AppendLine($"{indent}foreach ($script in $scriptsToExecute) {{");
        sb.AppendLine($"{indent}    Remove-Item -Path $script.Path -Force -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Removal scripts deleted\" \"INFO\"");
    }

    private static string AfterUpdateScriptContent(string scriptPaths) =>
        $$"""
        # Removes the Windows apps again once the staged Windows update has installed, then deletes its own task and the scripts.
        # The cleanup task waits for this one so two servicing operations never overlap; it is started last.
        if (Test-Path '{{ScriptPaths.UnattendUpdatesDirectory}}') { exit 0 }
        $scripts = @({{scriptPaths}})
        foreach ($path in $scripts) {
            Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -NoProfile -File `"$path`"" -Wait -NoNewWindow
        }
        Unregister-ScheduledTask -TaskName '{{AfterUpdateName}}' -TaskPath '\Winhance\' -Confirm:$false -ErrorAction SilentlyContinue
        Remove-Item -Path ($scripts + $PSCommandPath) -Force -ErrorAction SilentlyContinue
        Start-ScheduledTask -TaskPath '\Winhance\' -TaskName '{{CleanupScriptSection.TaskName}}' -ErrorAction SilentlyContinue

        """.ReplaceLineEndings("\n");

    private static string GenerateBloatRemovalScriptContent(AppRemovalPlan plan)
    {
        var includeXboxFix = GameBarRemoval.Includes(plan.Packages);

        return BloatRemovalScriptGenerator.GenerateScript([.. plan.Packages], [.. plan.Capabilities], [.. plan.Features], [.. plan.SpecialApps], includeXboxFix);
    }
}

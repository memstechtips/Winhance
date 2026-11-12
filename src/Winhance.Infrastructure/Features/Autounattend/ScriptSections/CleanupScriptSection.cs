using System.Text;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.WimUtil.Models;
using static Winhance.Infrastructure.Features.Autounattend.ScriptSections.EmbeddedScriptSection;

namespace Winhance.Infrastructure.Features.Autounattend.ScriptSections;

internal static class CleanupScriptSection
{
    internal const string TaskName = "WinhanceCleanup";

    public static void AppendCleanupTask(StringBuilder sb, ImageCleanupOptions options, string indent)
    {
        AppendEmbeddedScript(sb, TaskName, ScriptContent(options), indent);
        AppendRegisterStartupTask(sb, TaskName, indent);
        sb.AppendLine();
    }

    // The one-time task waits for a staged update to install, as the app removal one does, so the update's own
    // leftovers are cleared too. While that task is still registered this one steps aside: both service the running
    // system, and the app removal task starts this one when it finishes. A failed DISM leaves the task for the next boot.
    // Startup tasks begin a few seconds apart, so it also waits for any other running Winhance task (the Edge removal
    // runs DISM too) after giving them a minute to start.
    private static string ScriptContent(ImageCleanupOptions options)
    {
        var script = new StringBuilder();
        script.AppendLine("# Frees disk space once the staged Windows update has installed, then deletes its own task and script.");
        script.AppendLine($"if (Test-Path '{ScriptPaths.UnattendUpdatesDirectory}') {{ exit 0 }}");
        script.AppendLine($"if (Get-ScheduledTask -TaskPath '\\Winhance\\' -TaskName '{AppRemovalScriptSection.AfterUpdateName}' -ErrorAction SilentlyContinue) {{ exit 0 }}");
        script.AppendLine("Start-Sleep -Seconds 60");
        script.AppendLine($"while (Get-ScheduledTask -TaskPath '\\Winhance\\' -ErrorAction SilentlyContinue | Where-Object {{ $_.TaskName -ne '{TaskName}' -and $_.State -eq 'Running' }}) {{ Start-Sleep -Seconds 15 }}");
        script.AppendLine($"dism.exe /Online /Cleanup-Image /StartComponentCleanup{(options.ResetBase ? " /ResetBase" : "")} | Out-Null");
        script.AppendLine("if ($LASTEXITCODE -notin 0, 3010) { exit 1 }");

        if (options.DownloadedUpdates)
            AppendClear(script, @"C:\Windows\SoftwareDistribution\Download\*", recurse: true);
        if (options.ServicingLogs)
        {
            AppendClear(script, @"C:\Windows\Logs\CBS\*.cab", recurse: false);
            AppendClear(script, @"C:\Windows\Logs\DISM\*", recurse: true);
        }
        if (options.TempFiles)
            AppendClear(script, @"C:\Windows\Temp\*", recurse: true);
        if (options.CrashReports)
        {
            AppendClear(script, @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive\*", recurse: true);
            AppendClear(script, @"C:\ProgramData\Microsoft\Windows\WER\ReportQueue\*", recurse: true);
        }

        script.AppendLine($"Unregister-ScheduledTask -TaskName '{TaskName}' -TaskPath '\\Winhance\\' -Confirm:$false -ErrorAction SilentlyContinue");
        script.AppendLine("Remove-Item -Path $PSCommandPath -Force -ErrorAction SilentlyContinue");
        return script.ToString().ReplaceLineEndings("\n");
    }

    private static void AppendClear(StringBuilder script, string path, bool recurse) =>
        script.AppendLine($"Remove-Item -Path '{path}'{(recurse ? " -Recurse" : "")} -Force -ErrorAction SilentlyContinue");
}

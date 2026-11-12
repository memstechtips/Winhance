using System.Text;
using FluentAssertions;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class CleanupScriptSectionTests
{
    private static readonly ImageCleanupOptions Everything = new(true, true, true, true, true);
    private static readonly string[] EveryBoxMarker = ["/ResetBase", "SoftwareDistribution", "Logs\\CBS", "Windows\\Temp", "WER\\ReportQueue"];

    private static string Output(ImageCleanupOptions options, string indent = "")
    {
        var sb = new StringBuilder();
        CleanupScriptSection.AppendCleanupTask(sb, options, indent);
        return sb.ToString();
    }

    [Fact]
    public void AppendCleanupTask_Always_WritesTheScriptAndRegistersOneStartupTaskThatRunsIt()
    {
        var output = Output(Everything);

        output.Should().Contain("$winhanceCleanupContent = @'");
        output.Should().Contain("Join-Path $scriptsDir \"WinhanceCleanup.ps1\"");
        output.Should().Contain("New-ScheduledTaskTrigger -AtStartup");
        output.Should().Contain("-TaskName \"WinhanceCleanup\" -TaskPath \"\\Winhance\"");
        output.Should().Contain("-UserId \"SYSTEM\"");
    }

    [Fact]
    public void AppendCleanupTask_Always_WaitsForTheStagedUpdateBeforeCleaningAndThenRemovesItself()
    {
        var script = Output(Everything);

        script.IndexOf(@"if (Test-Path 'C:\ProgramData\Winhance\Unattend\Updates') { exit 0 }", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("dism.exe", StringComparison.Ordinal));
        script.Should().Contain("Unregister-ScheduledTask -TaskName 'WinhanceCleanup' -TaskPath '\\Winhance\\'");
        script.Should().Contain("Remove-Item -Path $PSCommandPath");
    }

    [Fact]
    public void AppendCleanupTask_Always_StepsAsideWhileTheAppRemovalTaskIsStillRegistered()
    {
        var script = Output(Everything);

        var wait = script.IndexOf("if (Get-ScheduledTask -TaskPath '\\Winhance\\' -TaskName 'WinhanceRemoveAppsAfterUpdate' -ErrorAction SilentlyContinue) { exit 0 }", StringComparison.Ordinal);
        wait.Should().BeGreaterThan(-1).And.BeLessThan(script.IndexOf("dism.exe", StringComparison.Ordinal));
    }

    [Fact]
    public void AppendCleanupTask_Always_WaitsForOtherRunningWinhanceTasksBeforeDism()
    {
        var script = Output(Everything);

        var sleep = script.IndexOf("Start-Sleep -Seconds 60", StringComparison.Ordinal);
        var wait = script.IndexOf("while (Get-ScheduledTask -TaskPath '\\Winhance\\' -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -ne 'WinhanceCleanup' -and $_.State -eq 'Running' }) { Start-Sleep -Seconds 15 }", StringComparison.Ordinal);
        sleep.Should().BeGreaterThan(script.IndexOf("-TaskName 'WinhanceRemoveAppsAfterUpdate'", StringComparison.Ordinal));
        wait.Should().BeGreaterThan(sleep).And.BeLessThan(script.IndexOf("dism.exe", StringComparison.Ordinal));
    }

    [Fact]
    public void AppendCleanupTask_Always_LeavesTheTaskInPlaceWhenDismFails()
    {
        var script = Output(Everything);

        var check = script.IndexOf("if ($LASTEXITCODE -notin 0, 3010) { exit 1 }", StringComparison.Ordinal);
        check.Should().BeGreaterThan(script.IndexOf("dism.exe", StringComparison.Ordinal))
            .And.BeLessThan(script.IndexOf("Remove-Item -Path 'C:", StringComparison.Ordinal))
            .And.BeLessThan(script.IndexOf("Unregister-ScheduledTask", StringComparison.Ordinal));
    }

    [Fact]
    public void AppendCleanupTask_EveryBoxTicked_ClearsEveryFolderTheImageCleanupClears()
    {
        var script = Output(Everything);

        script.Should().Contain("dism.exe /Online /Cleanup-Image /StartComponentCleanup /ResetBase");
        script.Should().Contain(@"'C:\Windows\SoftwareDistribution\Download\*'");
        script.Should().Contain(@"'C:\Windows\Logs\CBS\*.cab'");
        script.Should().Contain(@"'C:\Windows\Logs\DISM\*'");
        script.Should().Contain(@"'C:\Windows\Temp\*'");
        script.Should().Contain(@"'C:\ProgramData\Microsoft\Windows\WER\ReportArchive\*'");
        script.Should().Contain(@"'C:\ProgramData\Microsoft\Windows\WER\ReportQueue\*'");
    }

    [Fact]
    public void AppendCleanupTask_NoBoxTicked_StillCleansTheComponentStoreButKeepsUpdatesUninstallable()
    {
        var script = Output(ImageCleanupOptions.None);

        script.Should().Contain("dism.exe /Online /Cleanup-Image /StartComponentCleanup | Out-Null");
        script.Should().NotContain("/ResetBase");
        script.Should().NotContain("Remove-Item -Path 'C:");
    }

    [Theory]
    [InlineData(true, false, false, false, false, "/ResetBase")]
    [InlineData(false, true, false, false, false, "SoftwareDistribution")]
    [InlineData(false, false, true, false, false, "Logs\\CBS")]
    [InlineData(false, false, false, true, false, "Windows\\Temp")]
    [InlineData(false, false, false, false, true, "WER\\ReportQueue")]
    public void AppendCleanupTask_OneBoxTicked_CleansOnlyThatBox(bool resetBase, bool downloads, bool logs, bool temp, bool crashes, string expected)
    {
        var script = Output(new ImageCleanupOptions(resetBase, downloads, logs, temp, crashes));

        script.Should().Contain(expected);
        foreach (var other in EveryBoxMarker.Where(o => o != expected))
            script.Should().NotContain(other);
    }

    [Fact]
    public void AppendCleanupTask_WithIndent_KeepsItOnEveryLineOutsideTheHereString()
    {
        var output = Output(Everything, "    ");

        output.Should().Contain("    $winhanceCleanupContent = @'");
        output.Should().Contain("    try {");
    }
}

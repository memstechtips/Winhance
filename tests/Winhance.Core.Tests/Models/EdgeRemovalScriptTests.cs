using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class EdgeRemovalScriptTests
{
    [Fact]
    public void GetScript_ByDefault_KeepsWebView2AndEdgeUpdate()
    {
        var script = EdgeRemovalScript.GetScript();

        EdgeRemovalScript.RemovesWebView2(script).Should().BeFalse();
        script.Should().Contain("$RemoveWebView2 = $false");
    }

    [Fact]
    public void GetScript_WithWebView2_RecordsTheFullMode()
    {
        EdgeRemovalScript.RemovesWebView2(EdgeRemovalScript.GetScript(removeWebView2: true)).Should().BeTrue();
    }

    [Fact]
    public void RemovesWebView2_ScriptWrittenBeforeTheModeExisted_IsEdgeOnly()
    {
        EdgeRemovalScript.RemovesWebView2("<#\n  Script Version: 1.6\n#>").Should().BeFalse();
    }

    [Fact]
    public void Catalog_WebView2IsAWindowsAppRunningTheFullScript()
    {
        var windowsApps = WindowsAppDefinitions.GetWindowsApps().Items;
        var webView2 = windowsApps.Single(a => a.Id == "windows-app-edge-webview");
        var edge = windowsApps.Single(a => a.Id == "windows-app-edge");

        webView2.HasInstabilityWarning.Should().BeTrue();
        EdgeRemovalScript.RemovesWebView2(webView2.RemovalScript!()).Should().BeTrue();
        EdgeRemovalScript.RemovesWebView2(edge.RemovalScript!()).Should().BeFalse();
        ExternalAppDefinitions.GetExternalApps().Items
            .Should().NotContain(a => a.WinGetPackageId != null && a.WinGetPackageId.Contains("Microsoft.EdgeWebView2Runtime"));
    }

    [Fact]
    public void GetScript_BothModes_StopWebView2AndItsHostsOnlyBehindTheModeCheck()
    {
        foreach (var removeWebView2 in new[] { false, true })
        {
            var stop = StopEdgeProcesses(EdgeRemovalScript.GetScript(removeWebView2));
            var guard = stop.IndexOf("if ($RemoveWebView2)", StringComparison.Ordinal);

            guard.Should().BeGreaterThan(0);
            foreach (var process in new[] { "msedgewebview2", "OneDrive", "WidgetService", "Widgets" })
            {
                stop.IndexOf("\"" + process + "\"", StringComparison.Ordinal).Should().BeGreaterThan(guard, process);
            }
        }
    }

    [Theory]
    [InlineData(false, "$RemoveWebView2 = $false")]
    [InlineData(true, "$RemoveWebView2 = $true")]
    public void GetScript_SetsTheModeBeforeAnyFunctionReadsIt(bool removeWebView2, string modeLine)
    {
        var script = EdgeRemovalScript.GetScript(removeWebView2);

        script.IndexOf(modeLine, StringComparison.Ordinal).Should().BePositive()
            .And.BeLessThan(script.IndexOf("function Stop-EdgeProcesses", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_EdgeUpdateTasksAreSkippedOnlyWhileWebView2Stays()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().Contain("-like \"MicrosoftEdgeUpdate*\" -and -not $RemoveWebView2");
    }

    [Fact]
    public void GetScript_WebView2Cleanup_RunsOnlyInFullMode()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().Contain("if ($RemoveWebView2 -and (Test-WebView2Installed))");
    }

    [Fact]
    public void GetScript_EdgeAlreadyGone_StillCleansTheLeftoverUninstallEntry()
    {
        var script = EdgeRemovalScript.GetScript();

        var branch = script.IndexOf("} elseif (Test-Path \"HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge\")", StringComparison.Ordinal);
        branch.Should().BeGreaterThan(script.IndexOf("if ($removedSomething)", StringComparison.Ordinal));
        script.IndexOf("Remove-EdgeRegistryKeys", branch, StringComparison.Ordinal).Should().BePositive()
            .And.BeLessThan(script.IndexOf("if ($RemoveWebView2 -and", branch, StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_TaskbarPins_AreRemovedOnlyOnceEdgeIsGone()
    {
        var script = EdgeRemovalScript.GetScript();

        var guard = script.IndexOf("if (-not (Test-ChromiumEdgeInstalled)) {", StringComparison.Ordinal);
        guard.Should().BePositive();
        script.IndexOf("Remove-EdgeTaskbarPins", guard, StringComparison.Ordinal).Should().BeGreaterThan(guard);
        script.IndexOf("Remove-EdgeTaskbarPins", guard, StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("Checking for Edge scheduled tasks", guard, StringComparison.Ordinal));
        guard.Should().BeGreaterThan(script.IndexOf("\nInstall-EdgeProtocolRedirect", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_TaskbarPins_LoadEachSignedOutHiveOnlyOnce()
    {
        var script = EdgeRemovalScript.GetScript();
        var start = script.IndexOf("function Remove-EdgeTaskbarPins", StringComparison.Ordinal);
        var pins = script[start..script.IndexOf("# Function to remove Chromium Edge", start, StringComparison.Ordinal)];
        var load = pins.IndexOf("reg.exe load", StringComparison.Ordinal);

        pins.IndexOf("Get-ItemProperty $doneKey -Name $sid", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(load);
        pins.IndexOf("Test-Path \"Registry::HKEY_USERS\\WinhanceEdgePin\"", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(load);
        pins.IndexOf("New-ItemProperty $doneKey -Name $sid", StringComparison.Ordinal).Should().BeGreaterThan(load);
        pins.IndexOf("if ($removedSomething) { Remove-Item $doneKey", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(load);
        pins.Should().Contain("Could not unload the registry hive of $sid");
    }

    private static string StopEdgeProcesses(string script)
    {
        var start = script.IndexOf("function Stop-EdgeProcesses", StringComparison.Ordinal);
        return script[start..script.IndexOf("# Function to remove Legacy Edge", start, StringComparison.Ordinal)];
    }
}

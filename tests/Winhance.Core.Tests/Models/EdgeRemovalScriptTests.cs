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

        var branch = script.IndexOf("} elseif (Test-Path \"$sw\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge\")", StringComparison.Ordinal);
        branch.Should().BeGreaterThan(script.IndexOf("if ($removedSomething)", StringComparison.Ordinal));
        script.IndexOf("Remove-EdgeRegistryKeys", branch, StringComparison.Ordinal).Should().BePositive()
            .And.BeLessThan(script.IndexOf("if ($RemoveWebView2 -and", branch, StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_MicrosoftEdgeKeyOwnedByTrustedInstaller_IsDeletedAfterTakingOwnership()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().Contain("\"$sw\\Microsoft\\Windows\\CurrentVersion\\MicrosoftEdge\"");
        script.Should().Contain("function Remove-OwnedRegistryKey");
        script.Should().Contain("[System.Diagnostics.Process].GetMethod('SetPrivilege'")
            .And.NotContain("Add-Type -TypeDefinition", "the image build could not compile the P/Invoke");
        script.IndexOf("Remove-OwnedRegistryKey $path", StringComparison.Ordinal)
            .Should().BeGreaterThan(script.IndexOf("Remove-Item $path -Recurse -Force", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_MsEdgeHtmRedirect_PassesOnlyOneQuotePairToRegExe()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().Contain("/ve /d \"\"\"`\"$runtimeStubPath`\" %1\"");
        script.Should().NotContain("$runtimeStubPath`\"\"\" %1");
    }

    [Fact]
    public void GetScript_TaskbarPins_AreRemovedOnlyOnceEdgeIsGone()
    {
        var script = EdgeRemovalScript.GetScript();

        var guard = script.IndexOf("if (-not $Offline -and -not (Test-ChromiumEdgeInstalled)) {", StringComparison.Ordinal);
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
        var pins = script[start..script.IndexOf("function Remove-ChromiumEdge", start, StringComparison.Ordinal)];
        var load = pins.IndexOf("reg.exe load", StringComparison.Ordinal);

        pins.IndexOf("Get-ItemProperty $doneKey -Name $sid", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(load);
        pins.IndexOf("Test-Path \"Registry::HKEY_USERS\\WinhanceEdgePin\"", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(load);
        pins.IndexOf("New-ItemProperty $doneKey -Name $sid", StringComparison.Ordinal).Should().BeGreaterThan(load);
        pins.IndexOf("if ($removedSomething) { Remove-Item $doneKey", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(load);
        pins.Should().Contain("Could not unload the registry hive of $sid");
    }

    [Fact]
    public void GetScript_StopsTheUpdaterBeforeTheUninstallerAndDeprovisionsEdgeBeforeRemovingIt()
    {
        var script = EdgeRemovalScript.GetScript();
        var stop = StopEdgeProcesses(script);
        var chromium = script[script.IndexOf("function Remove-ChromiumEdge", StringComparison.Ordinal)..];

        stop.Should().Contain("Stop-Service -Name \"edgeupdate\", \"edgeupdatem\"");
        chromium.IndexOf("Stop-EdgeProcesses", StringComparison.Ordinal).Should().BePositive()
            .And.BeLessThan(chromium.IndexOf("Executing standard uninstaller", StringComparison.Ordinal));
        var deprovision = chromium.IndexOf("Remove-AppxProvisionedPackage -Online", StringComparison.Ordinal);
        deprovision.Should().BePositive()
            .And.BeGreaterThan(chromium.IndexOf("Executing standard uninstaller", StringComparison.Ordinal))
            .And.BeLessThan(chromium.IndexOf("Remove-AppxPackage -AllUsers", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_TakesTheImageAndHiveRootsWithLiveDefaults()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().Contain("param([string]$Image = '',");
        script.Should().Contain("[string]$SoftwareRoot = 'HKEY_LOCAL_MACHINE\\SOFTWARE'");
        script.Should().Contain("[string]$SystemRoot = 'HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet'");
        script.Should().Contain("[string]$UserRoot = 'HKEY_CURRENT_USER'");
        script.Should().Contain("$root = if ($Offline) { $Image.TrimEnd('\\') } else { $env:SystemDrive }");
        script.Should().Contain("If (-not $Image -and !(");
    }

    [Fact]
    public void GetScript_OnAnImage_SkipsEverythingThatNeedsARunningSystem()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().Contain("Remove-Path \"$programFiles\\Microsoft\\Edge\"");
        script.Should().Contain("if (-not $Offline -and -not (Test-ChromiumEdgeInstalled)) {");
        script.Should().Contain("if ($Offline) { Write-Host $Message; return }");
        script.Should().Contain("if (-not $Offline) { New-Item -ItemType Directory -Path $logFolder");
    }

    private static string StopEdgeProcesses(string script)
    {
        var start = script.IndexOf("function Stop-EdgeProcesses", StringComparison.Ordinal);
        return script[start..script.IndexOf("function Remove-LegacyEdge", start, StringComparison.Ordinal)];
    }

    [Fact]
    public void GetScript_NeverTakesTheStubFromPublicOrProgramData()
    {
        var script = EdgeRemovalScript.GetScript();

        script.Should().NotContain("$programData\\ie_to_edge_stub.exe");
        script.Should().NotContain("$users\\Public\\ie_to_edge_stub.exe");
        script.Should().Contain("Get-ChildItem \"$x86\\Microsoft\\Edge\" -Filter ie_to_edge_stub.exe");
        script.IndexOf("Test-Path $savedStub", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("Get-ChildItem \"$x86\\Microsoft\\Edge\" -Filter ie_to_edge_stub.exe", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_CopiesOnlyAMicrosoftSignedStub()
    {
        var script = EdgeRemovalScript.GetScript();
        var check = script.IndexOf("$signature.Status -eq 'Valid'", StringComparison.Ordinal);

        check.Should().BeGreaterThan(0);
        script.Should().Contain("$signature.SignerCertificate.Subject -match 'O=Microsoft Corporation'");
        script.IndexOf("Copy-Item $stubPath $savedStub", StringComparison.Ordinal).Should().BeGreaterThan(check);
    }
}

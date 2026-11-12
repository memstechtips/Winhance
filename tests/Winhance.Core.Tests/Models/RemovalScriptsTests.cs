using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class RemovalScriptsTests
{
    private static readonly Regex StopVerb = new(
        @"Stop-Process|taskkill|\.Kill\(\)|Terminate|Stop-Service", RegexOptions.IgnoreCase);

    private static readonly Regex ExplorerName = new(@"explorer", RegexOptions.IgnoreCase);

    // A stop verb counts as aimed at Explorer when Explorer is named on its line or in the few lines above, which
    // covers "query explorer.exe, then Stop-Process -Id". Reading Explorer's owner has no stop verb and stays allowed.
    private static bool StopsExplorer(string script)
    {
        var lines = script.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!StopVerb.IsMatch(lines[i])) continue;
            var window = string.Join('\n', lines[Math.Max(0, i - 4)..(i + 1)]);
            if (ExplorerName.IsMatch(window)) return true;
        }
        return false;
    }

    public static TheoryData<string, string> Scripts() => new()
    {
        { "Edge", EdgeRemovalScript.GetScript() },
        { "Edge with WebView2", EdgeRemovalScript.GetScript(removeWebView2: true) },
        { "OneDrive", OneDriveRemovalScript.GetScript() },
        {
            "Bloat",
            BloatRemovalScriptGenerator.GenerateScript(
                ["Microsoft.BingWeather"], ["Browser.InternetExplorer~~~~0.0.11.0"], ["MediaPlayback"], ["OneNote"],
                includeXboxRegistryFix: true, includeTeamsProcessKill: true)
        },
    };

    [Theory]
    [MemberData(nameof(Scripts))]
    public void Script_NeverStopsOrRestartsExplorer(string name, string script)
    {
        StopsExplorer(script).Should().BeFalse($"the {name} removal script must leave the shell running");
    }

    [Theory]
    [InlineData("Stop-Process -Name explorer -Force")]
    [InlineData("taskkill /f /im explorer.exe")]
    [InlineData("(Get-Process explorer).Kill()")]
    [InlineData("Get-Process explorer | Stop-Process -Force")]
    [InlineData("foreach ($process in Get-CimInstance Win32_Process -Filter \"Name='explorer.exe'\") {\n    $owner = Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid\n    Stop-Process -Id $process.ProcessId -Force\n}")]
    [InlineData("Get-CimInstance Win32_Process -Filter \"Name='explorer.exe'\" | Invoke-CimMethod -MethodName Terminate")]
    public void StopsExplorer_DetectsEveryWayOfEndingTheShell(string snippet)
    {
        StopsExplorer(snippet).Should().BeTrue();
    }

    [Fact]
    public void StopsExplorer_AllowsReadingTheExplorerOwner()
    {
        StopsExplorer("$explorer = Get-Process explorer | Select-Object -First 1\n$explorer.GetOwner().User").Should().BeFalse();
    }
}

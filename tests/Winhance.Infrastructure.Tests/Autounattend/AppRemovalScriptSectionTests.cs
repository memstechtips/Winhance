using System.Text;
using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Autounattend.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class AppRemovalScriptSectionTests
{
    private static readonly string[] CortanaPackage = ["Microsoft.549981C3F5F10"];
    private static readonly string[] EdgePackage = ["Microsoft.Edge"];
    private static readonly string[] OneDrivePackage = ["Microsoft.OneDrive"];
    private static readonly string[] XboxPackages = ["Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay"];

    private readonly AppRemovalScriptSection _sut = new();

    [Fact]
    public void AppendScriptsDirectorySetup_ContainsScriptsDirectory()
    {
        var sb = new StringBuilder();

        _sut.AppendScriptsDirectorySetup(sb);

        var output = sb.ToString();
        output.Should().Contain("$scriptsDir");
        output.Should().Contain("C:\\ProgramData\\Winhance\\Scripts");
    }

    [Fact]
    public void AppendScriptsDirectorySetup_ContainsDirectoryCreation()
    {
        var sb = new StringBuilder();

        _sut.AppendScriptsDirectorySetup(sb);

        var output = sb.ToString();
        output.Should().Contain("New-Item");
        output.Should().Contain("-ItemType Directory");
    }

    [Fact]
    public void AppendScriptsDirectorySetup_ContainsExistenceCheck()
    {
        var sb = new StringBuilder();

        _sut.AppendScriptsDirectorySetup(sb);

        sb.ToString().Should().Contain("Test-Path");
    }

    [Fact]
    public void AppendScriptsDirectorySetup_UsesProvidedIndent()
    {
        var sb = new StringBuilder();

        _sut.AppendScriptsDirectorySetup(sb, "        ");

        sb.ToString().Should().Contain("        $scriptsDir");
    }

    [Fact]
    public void AppendScriptsDirectorySetup_DefaultIndentIsEmpty()
    {
        var sb = new StringBuilder();

        _sut.AppendScriptsDirectorySetup(sb);

        var lines = sb.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("$scriptsDir");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_RegularApps_EmitsRemovalSection()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("WINDOWS APPS REMOVAL");
        output.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_EdgeApp_EmitsEdgeRemoval()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-edge", "Microsoft Edge", EdgePackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("EdgeRemoval");
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task AppendBloatRemovalScriptAsync_EdgeScript_CarriesTheWebView2Mode(bool webView, bool edge, bool removesWebView2)
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>();
        if (edge)
            apps.Add(new AppChoice("windows-app-edge", "Microsoft Edge", EdgePackage, null, null, null));
        if (webView)
            apps.Add(new AppChoice("windows-app-edge-webview", "Microsoft Edge WebView2 and EdgeUpdate", null, null, null, "Microsoft.EdgeWebView2Runtime"));

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Split("# Create EdgeRemoval.ps1 script").Should().HaveCount(2);
        output.Should().Contain(removesWebView2 ? "$RemoveWebView2 = $true" : "$RemoveWebView2 = $false");
    }

    [Fact]
    public void Catalog_RemovalScriptAndItsName_AreSetTogether()
    {
        var items = WindowsAppDefinitions.GetWindowsApps().Items;

        items.Should().Contain(a => a.RemovalScript != null)
            .And.OnlyContain(a => (a.RemovalScript == null) == string.IsNullOrEmpty(a.RemovalScriptName));
    }

    [Fact]
    public void Catalog_ItemsSharingARemovalScript_AgreeOnItsTrigger()
    {
        var sharedScripts = WindowsAppDefinitions.GetWindowsApps().Items
            .Where(a => a.RemovalScriptName != null)
            .GroupBy(a => a.RemovalScriptName);

        sharedScripts.Should().OnlyContain(g => g.Select(a => a.RemovalScriptRunsAtStartup).Distinct().Count() == 1);
    }

    [Theory]
    [InlineData("windows-app-edge", "Startup")]
    [InlineData("windows-app-edge-webview", "Startup")]
    [InlineData("windows-app-onedrive", "Logon")]
    public async Task AppendBloatRemovalScriptAsync_DedicatedScript_UsesTheCatalogTrigger(string appId, string trigger)
    {
        var app = WindowsAppDefinitions.GetWindowsApps().Items.Single(a => a.Id == appId);
        var sb = new StringBuilder();

        await _sut.AppendBloatRemovalScriptAsync(sb, [new AppChoice(app.Id, app.Name, null, null, null, null)], "    ");

        sb.ToString().Should().Contain($"Name = \"{app.RemovalScriptName}\"; TriggerType = \"{trigger}\"");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_EveryDedicatedScriptApp_EmitsItsCatalogScriptName()
    {
        foreach (var app in WindowsAppDefinitions.GetWindowsApps().Items.Where(a => a.RemovalScriptName != null))
        {
            var sb = new StringBuilder();

            await _sut.AppendBloatRemovalScriptAsync(sb, [new AppChoice(app.Id, app.Name, null, null, null, null)], "    ");

            sb.ToString().Should().Contain($"# Create {app.RemovalScriptName}.ps1 script", because: app.Id);
            sb.ToString().Should().Contain($"Name = \"{app.RemovalScriptName}\"", because: app.Id);
        }
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_OneDriveApp_EmitsOneDriveRemoval()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-onedrive", "OneDrive", OneDrivePackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("OneDriveRemoval");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_Capability_IncludedInScript()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-cap-wordpad", "WordPad", null, "Microsoft.Windows.WordPad~~~~0.0.1.0", null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_OptionalFeature_IncludedInScript()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-opt-ie", "Internet Explorer", null, null, "Internet-Explorer-Optional-amd64", null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_AppWithMultiplePackages_IncludesAllPackages()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-xbox", "Xbox", XboxPackages, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_EmitsScheduledTaskRegistration()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("Register-ScheduledTask");
        output.Should().Contain("Winhance");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_MixedApps_EmitsAllSections()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null),
            new AppChoice("windows-app-edge", "Microsoft Edge", EdgePackage, null, null, null),
            new AppChoice("windows-app-onedrive", "OneDrive", OneDrivePackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ");

        var output = sb.ToString();
        output.Should().Contain("BloatRemoval");
        output.Should().Contain("EdgeRemoval");
        output.Should().Contain("OneDriveRemoval");
    }

    private async Task<string> RemovalFor(AppRemovalMode mode)
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null),
            new AppChoice("windows-app-onedrive", "OneDrive", OneDrivePackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ", mode);

        return sb.ToString();
    }

    [Theory]
    [InlineData(nameof(AppRemovalMode.Once))]
    [InlineData(nameof(AppRemovalMode.Scripts))]
    [InlineData(nameof(AppRemovalMode.Winhance))]
    public async Task AppendBloatRemovalScriptAsync_WhenImageAlreadyRemovedApps_EveryModeSkipsTheRemovalRun(string modeName)
    {
        var output = await RemovalFor(Enum.Parse<AppRemovalMode>(modeName));

        output.Should().Contain("$appsRemovedInImage = Test-Path '");
        output.Should().Contain("if (-not $appsRemovedInImage -or $script.AfterImage) {");
    }

    // The image build cannot reach Edge's redirect, taskbar pins or update tasks, so Edge runs again.
    [Fact]
    public async Task AppendBloatRemovalScriptAsync_AfterImageRemovedApps_OnlyTheEdgeScriptRunsAgain()
    {
        var sb = new StringBuilder();
        var apps = new List<AppChoice>
        {
            new AppChoice("windows-app-edge", "Microsoft Edge", EdgePackage, null, null, null),
            new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null)
        };

        await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ", AppRemovalMode.Scripts);
        var entries = sb.ToString().Split('\n').Where(l => l.Contains("$scriptsToExecute += ")).ToList();

        entries.Should().ContainSingle(l => l.Contains("AfterImage = $true")).Which.Should().Contain("Name = \"EdgeRemoval\"");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_OneDriveApp_RunsAfterTheImageToo()
    {
        var sb = new StringBuilder();

        await _sut.AppendBloatRemovalScriptAsync(sb, [new AppChoice("windows-app-onedrive", "OneDrive", OneDrivePackage, null, null, null)], "    ", AppRemovalMode.Once);
        var entries = sb.ToString().Split('\n').Where(l => l.Contains("$scriptsToExecute += ")).ToList();

        entries.Should().ContainSingle().Which.Should().Contain($"Name = \"{OneDriveRemovalScript.ScriptName}\"").And.Contain("AfterImage = $true");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_OnceMode_StartsTheCleanupTaskLastAfterRemovingItself()
    {
        var output = await RemovalFor(AppRemovalMode.Once);

        var unregister = output.IndexOf("Unregister-ScheduledTask -TaskName 'WinhanceRemoveAppsAfterUpdate'", StringComparison.Ordinal);
        var start = output.IndexOf("Start-ScheduledTask -TaskPath '\\Winhance\\' -TaskName 'WinhanceCleanup' -ErrorAction SilentlyContinue", StringComparison.Ordinal);
        unregister.Should().BeGreaterThan(-1);
        start.Should().BeGreaterThan(unregister);
    }

    // The image build cannot run a registry uninstall, so OneNote goes through the Bloat script on the installed system.
    [Fact]
    public async Task AppendBloatRemovalScriptAsync_WithRegistryUninstallApp_RunsTheBloatScriptAfterTheImage()
    {
        async Task<string> BloatEntry(params AppChoice[] apps)
        {
            var sb = new StringBuilder();
            await _sut.AppendBloatRemovalScriptAsync(sb, apps, "    ", AppRemovalMode.Once);
            return sb.ToString().Split('\n').Single(l => l.Contains("$scriptsToExecute += ") && l.Contains("Name = \"BloatRemoval\""));
        }

        (await BloatEntry(new AppChoice("windows-app-onenote", "OneNote", ["Microsoft.Office.OneNote"], null, null, null)))
            .Should().Contain("AfterImage = $true");
        (await BloatEntry(new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null)))
            .Should().NotContain("AfterImage");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_ScriptsMode_RegistersATaskPerScriptAndOtherModesDoNot()
    {
        (await RemovalFor(AppRemovalMode.Scripts)).Should().Contain("Register-ScheduledTask -TaskName $script.Name");
        (await RemovalFor(AppRemovalMode.Once)).Should().NotContain("Register-ScheduledTask -TaskName $script.Name");
        (await RemovalFor(AppRemovalMode.Winhance)).Should().NotContain("Register-ScheduledTask");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_OnceMode_RegistersOneStartupTaskWhenUpdateStagedElseDeletesScripts()
    {
        var output = await RemovalFor(AppRemovalMode.Once);

        output.Should().Contain("if (Test-Path '");
        output.Should().Contain("-TaskName \"WinhanceRemoveAppsAfterUpdate\" -TaskPath \"\\Winhance\"");
        output.Should().Contain("New-ScheduledTaskTrigger -AtStartup");
        output.Should().Contain("} else {");
        output.Should().Contain("Remove-Item -Path $script.Path -Force");
        output.Should().NotContain("KeepRemoved");
        (await RemovalFor(AppRemovalMode.Scripts)).Should().NotContain("WinhanceRemoveAppsAfterUpdate");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_WinhanceMode_RecordsEachAppIdUnderKeepRemovedAndDeletesScripts()
    {
        var output = await RemovalFor(AppRemovalMode.Winhance);

        output.Should().Contain("$keptKey = 'Registry::HKEY_LOCAL_MACHINE\\SOFTWARE\\Winhance\\KeepRemoved'");
        output.Should().Contain("Set-ItemProperty -Path $keptKey -Name 'Mode' -Value 'Winhance' -Type String");
        output.Should().Contain("Set-ItemProperty -Path $keptKey -Name 'Enabled' -Value 1 -Type DWord");
        output.Should().Contain("foreach ($appId in @('windows-app-cortana', 'windows-app-onedrive'))");
        output.Should().Contain("Remove-Item -Path $script.Path -Force");
        output.Should().NotContain("WinhanceRemoveAppsAfterUpdate");
    }

    [Fact]
    public async Task AppendBloatRemovalScriptAsync_ScriptsMode_KeepsScriptsAndWritesNoKeptList()
    {
        var output = await RemovalFor(AppRemovalMode.Scripts);

        output.Should().NotContain("KeepRemoved");
        output.Should().NotContain("Remove-Item -Path $script.Path");
    }
}

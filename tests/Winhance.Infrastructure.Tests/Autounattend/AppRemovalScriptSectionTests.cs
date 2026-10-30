using System.Text;
using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Catalogs;
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
}

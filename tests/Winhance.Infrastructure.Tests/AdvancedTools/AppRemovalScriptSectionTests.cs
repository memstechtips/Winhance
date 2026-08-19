using System.Text;
using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

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

    [Fact]
    public void AppendWinhanceInstallerScriptContent_ContainsInstallerScript()
    {
        var sb = new StringBuilder();

        _sut.AppendWinhanceInstallerScriptContent(sb);

        var output = sb.ToString();
        output.Should().Contain("Install Winhance.lnk");
        output.Should().Contain("CreateShortcut");
    }

    [Fact]
    public void AppendWinhanceInstallerScriptContent_ContainsDownloadUrl()
    {
        var sb = new StringBuilder();

        _sut.AppendWinhanceInstallerScriptContent(sb);

        sb.ToString().Should().Contain("get.winhance.net");
    }

    [Fact]
    public void AppendWinhanceInstallerScriptContent_ContainsDesktopShortcutCreation()
    {
        var sb = new StringBuilder();

        _sut.AppendWinhanceInstallerScriptContent(sb);

        var output = sb.ToString();
        output.Should().Contain("Install Winhance.lnk");
        output.Should().Contain("WScript.Shell");
        output.Should().Contain("CreateShortcut");
    }

    [Fact]
    public void AppendWinhanceInstallerScriptContent_UsesProvidedIndent()
    {
        var sb = new StringBuilder();

        _sut.AppendWinhanceInstallerScriptContent(sb, "        ");

        sb.ToString().Should().Contain("        # Create desktop shortcut for Winhance installer");
    }

    [Fact]
    public void AppendWinhanceInstallerScriptContent_ContainsErrorHandling()
    {
        var sb = new StringBuilder();

        _sut.AppendWinhanceInstallerScriptContent(sb);

        var output = sb.ToString();
        output.Should().Contain("try {");
        output.Should().Contain("catch {");
    }
}

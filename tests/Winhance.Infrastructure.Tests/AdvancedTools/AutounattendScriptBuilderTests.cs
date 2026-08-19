using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class AutounattendScriptBuilderTests
{
    private static readonly string[] CortanaPackage = ["Microsoft.549981C3F5F10"];

    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly Mock<IWindowsVersionService> _windowsVersionService = new();
    private readonly AutounattendScriptBuilder _sut;

    public AutounattendScriptBuilderTests()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _sut = new AutounattendScriptBuilder(
            _logService.Object,
            _powerShellRunner.Object,
            _windowsVersionService.Object);
    }

    private static SelectionSet SetOf(params SettingChoice[] choices) =>
        new(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None);

    [Fact]
    public async Task BuildAsync_EmptySet_ProducesValidScript()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildAsync_ContainsHeader()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain(".SYNOPSIS");
        result.Should().Contain("param(");
    }

    [Fact]
    public async Task BuildAsync_ContainsLoggingSetup()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("function Write-Log");
        result.Should().Contain("$LogPath");
    }

    [Fact]
    public async Task BuildAsync_ContainsHelperFunctions()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("function Set-RegistryValue");
        result.Should().Contain("function Start-ProcessAsUser");
    }

    [Fact]
    public async Task BuildAsync_ContainsSystemBlock()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("if (-not $UserCustomizations)");
    }

    [Fact]
    public async Task BuildAsync_ContainsUserBlock()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("if ($UserCustomizations)");
    }

    [Fact]
    public async Task BuildAsync_ContainsCompletionBlock()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("Script Completed");
    }

    [Fact]
    public async Task BuildAsync_ContainsCustomScriptPlaceholders()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("SYSTEM WIDE");
        result.Should().Contain("USER SPECIFIC");
        result.Should().Contain("# Start here");
        result.Should().Contain("# End here");
    }

    [Fact]
    public async Task BuildAsync_ContainsScriptsDirectorySetup()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("$scriptsDir");
    }

    [Fact]
    public async Task BuildAsync_ContainsWinhanceInstaller()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("Install Winhance.lnk");
    }

    [Fact]
    public async Task BuildAsync_ContainsCleanStartMenu()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("START MENU LAYOUT");
    }

    [Fact]
    public async Task BuildAsync_ContainsUserCustomizationsTask()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("WinhanceUserCustomizations");
    }

    [Fact]
    public async Task BuildAsync_ContainsUserDetectionBridge()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("$runningAsSystem");
        result.Should().Contain("S-1-5-18");
        result.Should().Contain("UserCustomizationsApplied");
    }

    [Fact]
    public async Task BuildAsync_CallsValidateScriptSyntax()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildAsync(set, byFeature);

        _powerShellRunner.Verify(r => r.ValidateScriptSyntaxAsync(
            It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_SyntaxValidationFails_Throws()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Syntax error at line 42"));

        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var act = () => _sut.BuildAsync(set, byFeature);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Syntax error*");
    }

    [Fact]
    public async Task BuildAsync_WithWindowsApps_EmitsAppRemoval()
    {
        var set = new SelectionSet(
            Array.Empty<SettingChoice>(),
            new[] { new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null) },
            Array.Empty<AppChoice>(),
            AutounattendChoices.None);
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("WINDOWS APPS REMOVAL");
        result.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task BuildAsync_WithOptimizeFeatures_EmitsHklmRegistryEntries()
    {
        // The pipeline runs on the catalog Setting dict, so the fixture passes the REAL
        // catalog HKLM registry toggle (security-remote-assistance, RegTarget fAllowToGetHelp) directly.
        var set = SetOf(new SettingChoice("security-remote-assistance", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Privacy, new[] { SettingCatalog.Find("security-remote-assistance")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("Set-RegistryValue");
        result.Should().Contain("fAllowToGetHelp");
    }

    [Fact]
    public async Task BuildAsync_WithCustomizeFeatures_EmitsHkcuInUserBlock()
    {
        // The REAL catalog HKCU toggle (gaming-game-mode, RegTarget AutoGameModeEnabled under
        // HKEY_CURRENT_USER) rides the Setting dict directly. The value name lands only in the HKCU pass,
        // i.e. inside the $UserCustomizations block.
        var set = SetOf(new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.GamingPerformance, new[] { SettingCatalog.Find("gaming-game-mode")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        var userBlockIndex = result.IndexOf("if ($UserCustomizations)");
        var custValIndex = result.IndexOf("AutoGameModeEnabled", userBlockIndex);
        custValIndex.Should().BeGreaterThan(userBlockIndex);
    }

    [Fact]
    public async Task BuildAsync_ValidSyntax_LogsSuccess()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildAsync(set, byFeature);

        _logService.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(s => s.Contains("passed PowerShell syntax validation")),
            null), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_FailedSyntax_LogsError()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Bad syntax"));

        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        try { await _sut.BuildAsync(set, byFeature); }
        catch { }

        _logService.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(s => s.Contains("failed PowerShell syntax validation")),
            null), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_SettingDict_RealPowerSetting_EmitsCatalogPowerCfgTargets()
    {
        // A REAL catalog power setting rides the Setting dict end-to-end. A powercfg-only setting never reaches
        // either registry pass: the power section writes one @{ S=..; G=..; AC=..; DC=.. } row per catalog
        // PowerCfgTarget instead.
        var set = SetOf(new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(1, 0)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, new[] { SettingCatalog.Find("power-display-timeout")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("# POWER PLAN & POWERCFG SETTINGS");
        result.Should().Contain("S=\"7516b95f-f776-4464-8c53-06167f40cc99\"; G=\"3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e\"");
    }

    [Fact]
    public async Task BuildAsync_SettingDict_AliasedConfigId_EmitsViaMergedCatalogSetting()
    {
        // A set restored from a Windows-10 export carries the retired "-win10" This PC folder item id, while the
        // Setting dict (fed straight from the catalog registry) carries only the MERGED catalog setting under its
        // canonical id. What this fact PINS red-on-mutation is the emitter's alias-NORMALIZED choice lookup - so
        // the toggle still emits, via the Win10 KeyExists target: the ctor's IWindowsVersionService mock reports
        // build 0, which falls inside BuildRange.Windows10, so the threaded build drops the Win11
        // HiddenByDefault target.
        var set = SetOf(new SettingChoice("explorer-customization-thispc-folder-desktop-win10", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.ExplorerCustomization, new[] { SettingCatalog.Find("explorer-customization-thispc-folder-desktop")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");
        result.Should().NotContain("HiddenByDefault");
    }
}

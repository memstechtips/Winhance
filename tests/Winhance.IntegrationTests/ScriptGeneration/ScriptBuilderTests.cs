using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

[Trait("Category", "Integration")]
public class ScriptBuilderTests
{
    private static readonly string[] ClipchampPackage = ["Clipchamp.Clipchamp"];
    private static readonly string[] TestAppPackages = ["Microsoft.TestApp", "Microsoft.TestApp.Sub1", "Microsoft.TestApp.Sub2"];

    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly AutounattendScriptBuilder _builder;

    public ScriptBuilderTests()
    {
        // PowerShell validation is a no-op in tests
        _powerShellRunner
            .Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _builder = new AutounattendScriptBuilder(
            _logService.Object,
            _powerShellRunner.Object,
            new Mock<IWindowsVersionService>().Object);
    }

    // Apps plus one setting of each shape the passes route differently: HKLM registry (system pass), HKCU registry
    // (user pass) and powercfg (the power section).
    private static SelectionSet FullSet() => new(
        new SettingChoice[]
        {
            new("security-remote-assistance", new ChoiceValue.Toggle(true)),
            new("gaming-game-mode", new ChoiceValue.Toggle(false)),
            new("power-display-timeout", new ChoiceValue.AcDcOption(1, 0)),
        },
        new[] { new AppChoice("app1", "Test Windows App", TestAppPackages, null, null, null) },
        new[] { new AppChoice("ext1", "External App", null, null, null, "TestVendor.TestApp") },
        AutounattendChoices.None);

    private static Dictionary<string, IReadOnlyList<Setting>> FullCatalog() => new()
    {
        [FeatureIds.Privacy] = new[] { SettingCatalog.Find("security-remote-assistance")! },
        [FeatureIds.GamingPerformance] = new[] { SettingCatalog.Find("gaming-game-mode")! },
        [FeatureIds.Power] = new[] { SettingCatalog.Find("power-display-timeout")! },
    };

    [Fact]
    public async Task Build_WithWindowsApps_ContainsAppRemoval()
    {
        var set = new SelectionSet(
            Array.Empty<SettingChoice>(),
            new[] { new AppChoice("app1", "Clipchamp", ClipchampPackage, null, null, null) },
            Array.Empty<AppChoice>(),
            AutounattendChoices.None);
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var script = await _builder.BuildAsync(set, byFeature);

        script.Should().Contain("Clipchamp.Clipchamp");
        script.Should().Contain("Get-AppxPackage");
    }

    [Fact]
    public async Task Build_Script_HasBalancedBraces()
    {
        var script = await _builder.BuildAsync(FullSet(), FullCatalog());

        var openBraces = script.Count(c => c == '{');
        var closeBraces = script.Count(c => c == '}');
        openBraces.Should().Be(closeBraces,
            $"script should have balanced braces but has {openBraces} open and {closeBraces} close");
    }

    [Fact]
    public async Task Build_Script_ContainsRequiredStructure()
    {
        var script = await _builder.BuildAsync(FullSet(), FullCatalog());

        script.Should().Contain("Write-Log");
        script.Should().Contain("$scriptsDir");
        script.Should().Contain("$UserCustomizations");
        script.Should().Contain("UserCustomizations");
    }

    [Fact]
    public async Task Build_EmptySet_ProducesMinimalScript()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var script = await _builder.BuildAsync(set, byFeature);

        script.Should().NotBeNullOrEmpty();
        script.Should().Contain("Write-Log");
        script.Should().Contain("if (-not $UserCustomizations)");
        script.Should().Contain("if ($UserCustomizations)");
    }

    [Fact]
    public async Task Build_WithOptimizeFeatures_ContainsRegistryCommands()
    {
        // The pipeline runs on the catalog Setting dict, so the fixture passes the REAL catalog toggle
        // security-remote-assistance (HKLM DWORD fAllowToGetHelp) directly; the emit reads the CATALOG RegTarget
        // and state values.
        var set = new SelectionSet(
            new[] { new SettingChoice("security-remote-assistance", new ChoiceValue.Toggle(true)) },
            Array.Empty<AppChoice>(),
            Array.Empty<AppChoice>(),
            AutounattendChoices.None);
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Privacy] = new[] { SettingCatalog.Find("security-remote-assistance")! },
        };

        var script = await _builder.BuildAsync(set, byFeature);

        script.Should().Contain("Set-RegistryValue");
        script.Should().Contain("fAllowToGetHelp");
    }

    [Fact]
    public async Task Build_WithPowerSettings_ContainsPowerCfgCommands()
    {
        // The REAL catalog setting power-display-timeout (PowerOptimizationsCatalog.cs:
        // subgroup 7516b95f-f776-4464-8c53-06167f40cc99, setting 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e,
        // no hardware gate) rides the Setting dict directly. A powercfg-only setting emits no registry line at
        // all - the power section writes one @{ S=..; G=..; AC=..; DC=.. } row per catalog PowerCfgTarget.
        var set = new SelectionSet(
            new[] { new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(1, 0)) },
            Array.Empty<AppChoice>(),
            Array.Empty<AppChoice>(),
            AutounattendChoices.None);
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Power] = new[] { SettingCatalog.Find("power-display-timeout")! },
        };

        var script = await _builder.BuildAsync(set, byFeature);

        script.Should().Contain("powercfg");
        script.Should().Contain("S=\"7516b95f-f776-4464-8c53-06167f40cc99\"; G=\"3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e\"");
    }
}

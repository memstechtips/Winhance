using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Infrastructure.Tests.Services;

public class BuilderSeedSourceTests
{
    private static readonly string[] TogglePath = [@"HKEY_CURRENT_USER\Software\T"];
    private static readonly string[] SelectionPath = [@"HKEY_CURRENT_USER\Software\S"];
    private static readonly StateRole[] NoRoles = [];
    private static readonly StateRole[] RecommendedRole = [StateRole.Recommended];
    private static readonly StateRole[] DefaultRole = [StateRole.WindowsDefault];
    private static readonly StateRole[] Windows10DefaultRole = [new StateRole(RoleKind.WindowsDefault) { AppliesTo = [BuildRange.Windows10] }];
    private static readonly StateRole[] Windows11DefaultRole = [new StateRole(RoleKind.WindowsDefault) { AppliesTo = [BuildRange.Windows11] }];
    private static readonly StateRole[] RecommendedAcRole = [new StateRole(RoleKind.Recommended, PowerContext.AC)];
    private static readonly StateRole[] RecommendedDcRole = [new StateRole(RoleKind.Recommended, PowerContext.DC)];
    private static readonly ContextValue[] TenAcFiveDc = [new ContextValue(PowerContext.AC, 10), new ContextValue(PowerContext.DC, 5)];

    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IWindowsVersionService> _version = new();
    private readonly Mock<ILogService> _log = new();

    public BuilderSeedSourceTests()
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _version.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        _version.Setup(v => v.GetWindowsBuildRevision()).Returns(4000);
    }

    private BuilderSeedSource Sut() => new(_registry.Object, _version.Object, _log.Object);

    private void Arrange(Setting setting) =>
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { setting } });

    private static Setting Toggle(StateRole[] enabled, StateRole[] disabled) => new()
    {
        Id = "t", Display = new() { Name = TestKeys.Of("t"), Description = TestKeys.Of("t") },
        Targets = new Target[] { new RegTarget("V", TogglePath, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Roles = enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Roles = disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting CheckBox(StateRole[] checkedRoles, StateRole[] uncheckedRoles) => new()
    {
        Id = "c", Display = new() { Name = TestKeys.Of("c"), Description = TestKeys.Of("c") },
        Targets = new Target[] { new RegTarget("V", TogglePath, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Checked, Roles = checkedRoles, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Unchecked, Roles = uncheckedRoles, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting Selection(StateRole[] first, StateRole[] second, StateRole[] third) => new()
    {
        Id = "s", Display = new() { Name = TestKeys.Of("s"), Description = TestKeys.Of("s") },
        Targets = new Target[] { new RegTarget("M", SelectionPath, "Mode", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("A"), Roles = first, Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
            new SettingState { Label = TestKeys.Of("B"), Roles = second, Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) } },
            new SettingState { Label = TestKeys.Of("C"), Roles = third, Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(2) } },
        },
    };

    // Payloads deliberately unequal to their option indices, as PowerOptions.TimeIntervals is: the seed must
    // record the option INDEX, and 0/1 would pass whichever of the two it recorded.
    private static Setting PowerCfgSelection(StateRole[] off, StateRole[] on) => new()
    {
        Id = "p", Display = new() { Name = TestKeys.Of("p"), Description = TestKeys.Of("p") },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub", "set", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("5 minutes"), Roles = off, Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(300) } },
            new SettingState { Label = TestKeys.Of("15 minutes"), Roles = on, Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(900) } },
        },
    };

    private static Setting Slider() => new()
    {
        Id = "n", Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("n") },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub", "set", PowerModeSupport.Separate) },
        Numeric = new() { Min = 0, Max = 120, Units = "minutes", Recommended = TenAcFiveDc },
    };

    private static readonly string[] EnvironmentPaths = [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"];

    private static Setting Architecture() => new()
    {
        Id = "arch", Display = new() { Name = TestKeys.Of("arch"), Description = TestKeys.Of("arch") },
        Targets = new Target[]
        {
            new AutounattendArchitecture("arch", "amd64"),
            new RegTarget("this-pc", EnvironmentPaths, "PROCESSOR_ARCHITECTURE", RegistryValueKind.String) { ReadOnly = true },
        },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Checked, Roles = Windows11DefaultRole, Set = new Dictionary<string, StateValue> { ["arch"] = StateValue.Of("amd64") } },
            new SettingState { Label = LocKey.Common.Unchecked, Set = new Dictionary<string, StateValue> { ["arch"] = StateValue.Absent } },
        },
    };

    private static Setting TextSetting(string? defaultValue) => new()
    {
        Id = "x", Display = new() { Name = TestKeys.Of("x"), Description = TestKeys.Of("x") },
        Targets = new Target[] { new AutounattendElement("K", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName") },
        TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), defaultValue),
    };

    [Fact]
    public async Task Recommended_Toggle_UsesRecommendedRole()
    {
        Arrange(Toggle(NoRoles, RecommendedRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("t", new ChoiceValue.Toggle(false)));
    }

    [Theory]
    [InlineData(26100, false)]
    [InlineData(19045, true)]
    public async Task WindowsDefaults_Toggle_UsesDefaultRole_BuildScoped(int buildNumber, bool expected)
    {
        _version.Setup(v => v.GetWindowsBuildNumber()).Returns(buildNumber);
        Arrange(Toggle(Windows10DefaultRole, Windows11DefaultRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.WindowsDefaults, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.Toggle(expected));
    }

    [Fact]
    public async Task WindowsDefaults_CheckBox_SeedsItsOwnShape()
    {
        Arrange(CheckBox(NoRoles, Windows11DefaultRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.WindowsDefaults, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.CheckBox(false));
    }

    [Fact]
    public async Task A_role_seed_leaves_a_card_seeded_from_this_PC_alone()
    {
        Arrange(Architecture());

        var choices = await Sut().ChoicesForAsync(BuilderSeed.WindowsDefaults, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    [Fact]
    public async Task Recommended_Selection_UsesRecommendedIndex()
    {
        Arrange(Selection(NoRoles, NoRoles, RecommendedRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.Option(2));
    }

    [Fact]
    public async Task WindowsDefaults_Selection_UsesDefaultIndex()
    {
        Arrange(Selection(NoRoles, DefaultRole, NoRoles));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.WindowsDefaults, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.Option(1));
    }

    [Fact]
    public async Task Recommended_PowerCfgSelection_UsesAcDcRecommendedIndices()
    {
        Arrange(PowerCfgSelection(RecommendedDcRole, RecommendedAcRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.AcDcOption(1, 0));
    }

    [Fact]
    public async Task Recommended_Slider_UsesNumericRecommendedInSystemUnits()
    {
        Arrange(Slider());

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.AcDcNumber(600, 300));
    }

    [Fact]
    public async Task WindowsDefaults_Text_SeedsTheSettingsOwnDefault()
    {
        Arrange(TextSetting("PC-01"));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.WindowsDefaults, CatalogScope.CurrentMachine);

        choices.Single().Value.Should().Be(new ChoiceValue.Text("PC-01"));
    }

    [Fact]
    public async Task Recommended_TextWithoutADefault_IsOmitted()
    {
        Arrange(TextSetting(null));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    [Fact]
    public async Task SettingWithoutThatRole_IsOmitted()
    {
        Arrange(Toggle(NoRoles, DefaultRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    [Fact]
    public async Task Action_IsOmitted()
    {
        Arrange(new Setting { Id = "a", Display = new() { Name = TestKeys.Of("a"), Description = TestKeys.Of("a") } });

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    // A keyed option is a fact about one machine: seeding it would write this PC's time zone into a Recommended config.
    [Fact]
    public async Task KeyedSelection_IsOmitted()
    {
        Arrange(SettingCatalog.Find("region-time-zone")!);

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    [Fact]
    public async Task PowerPlan_IsOmitted()
    {
        Arrange(ParityFixtures.PowerPlanSetting());

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    [Fact]
    public async Task CurrentMachine_IsEmpty()
    {
        Arrange(Toggle(NoRoles, RecommendedRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.CurrentMachine, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
        _registry.Verify(r => r.GetAll(It.IsAny<CatalogScope>()), Times.Never);
    }

    [Fact]
    public async Task Scope_IsForwardedToTheRegistry()
    {
        Arrange(Toggle(NoRoles, RecommendedRole));

        await Sut().ChoicesForAsync(BuilderSeed.Recommended, new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false));

        _registry.Verify(r => r.GetAll(new CatalogScope(true, false)), Times.Once);
    }
}

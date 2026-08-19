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
        _registry.Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { setting } });

    private static Setting Toggle(StateRole[] enabled, StateRole[] disabled) => new()
    {
        Id = "t", Display = new() { Name = "t", Description = "t" },
        Targets = new Target[] { new RegTarget("V", TogglePath, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "Enabled", Roles = enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = "Disabled", Roles = disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting Selection(StateRole[] first, StateRole[] second, StateRole[] third) => new()
    {
        Id = "s", Display = new() { Name = "s", Description = "s" },
        Targets = new Target[] { new RegTarget("M", SelectionPath, "Mode", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "A", Roles = first, Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
            new SettingState { Label = "B", Roles = second, Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) } },
            new SettingState { Label = "C", Roles = third, Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(2) } },
        },
    };

    private static Setting PowerCfgSelection(StateRole[] off, StateRole[] on) => new()
    {
        Id = "p", Display = new() { Name = "p", Description = "p" },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub", "set", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = "Off", Roles = off, Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(0) } },
            new SettingState { Label = "On", Roles = on, Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(1) } },
        },
    };

    private static Setting Slider() => new()
    {
        Id = "n", Display = new() { Name = "n", Description = "n" },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub", "set", PowerModeSupport.Separate) },
        Numeric = new() { Min = 0, Max = 120, Units = "minutes", Recommended = TenAcFiveDc },
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
    public async Task SettingWithoutThatRole_IsOmitted()
    {
        Arrange(Toggle(NoRoles, DefaultRole));

        var choices = await Sut().ChoicesForAsync(BuilderSeed.Recommended, CatalogScope.CurrentMachine);

        choices.Should().BeEmpty();
    }

    [Fact]
    public async Task Action_IsOmitted()
    {
        Arrange(new Setting { Id = "a", Display = new() { Name = "a", Description = "a" } });

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
        _registry.Verify(r => r.GetAll(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Scope_IsForwardedToTheRegistry()
    {
        Arrange(Toggle(NoRoles, RecommendedRole));

        await Sut().ChoicesForAsync(BuilderSeed.Recommended, new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false));

        _registry.Verify(r => r.GetAll(true), Times.Once);
    }
}

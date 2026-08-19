using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingSnapshotSourceTests
{
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ICatalogSettingStateProvider> _states = new();
    private readonly Mock<ILogService> _log = new();

    private SettingSnapshotSource Sut() => new(_registry.Object, _states.Object, _log.Object);

    private void Arrange(Setting setting, SettingStateResult state)
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { setting } });
        _states.Setup(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult> { [setting.Id] = state });
    }

    [Fact]
    public async Task Toggle_MapsIsEnabled()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = true, IsEnabled = true });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("t", new ChoiceValue.Toggle(true)));
    }

    [Fact]
    public async Task Action_MapsIsEnabledAsAToggle()
    {
        var action = new Setting { Id = "a", Display = new() { Name = "a", Description = "a" } };
        Arrange(action, new SettingStateResult { Success = true, IsEnabled = false });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Toggle(false));
    }

    [Fact]
    public async Task Selection_AtOption_MapsIndex()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult { Success = true, CurrentValue = 1 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Option(1));
    }

    [Fact]
    public async Task Selection_NonIntCurrentValue_MapsIndexZero()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult { Success = true, CurrentValue = null });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Option(0));
    }

    [Fact]
    public async Task Selection_AtCustom_MapsReadingsKeyedByValueName()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult
        {
            Success = true, CurrentValue = ComboBoxConstants.CustomStateIndex,
            Readings = new Dictionary<string, object?> { ["Mode"] = 7, ["Unrelated"] = 1 },
        });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value
            .Should().BeOfType<ChoiceValue.CustomValues>().Which.Values.Should().Equal(new Dictionary<string, object> { ["Mode"] = 7 });
    }

    [Fact]
    public async Task Selection_AtCustom_WithNoReadings_IsOmitted()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult { Success = true, CurrentValue = ComboBoxConstants.CustomStateIndex });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task PowerCfgSelection_Separate_MapsAcDcIndicesFromPayloads()
    {
        Arrange(ParityFixtures.PowerCfgSelection("p"), new SettingStateResult { Success = true, CurrentValue = 1, AcValue = 1, DcValue = 0 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcOption(1, 0));
    }

    [Fact]
    public async Task PowerCfgSelection_Separate_WithoutAcDcReadings_MapsIndex()
    {
        Arrange(ParityFixtures.PowerCfgSelection("p"), new SettingStateResult { Success = true, CurrentValue = 1 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Option(1));
    }

    [Fact]
    public async Task Slider_Separate_MapsRawSystemValues()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Separate), new SettingStateResult { Success = true, CurrentValue = 600, AcValue = 600, DcValue = 300 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcNumber(600, 300));
    }

    [Fact]
    public async Task Slider_Separate_NoDc_UsesAcForBoth()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Separate), new SettingStateResult { Success = true, CurrentValue = 600, AcValue = 600 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcNumber(600, 600));
    }

    [Fact]
    public async Task Slider_Both_MapsCurrentValue()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Both), new SettingStateResult { Success = true, CurrentValue = 600 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Number(600));
    }

    [Fact]
    public async Task Slider_NoCurrentValue_IsOmitted()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Separate), new SettingStateResult { Success = true });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task PowerPlan_MapsGuidAndName()
    {
        Arrange(ParityFixtures.PowerPlanSetting(), new SettingStateResult { Success = true, CurrentValue = 0, DynamicSelection = "guid-1", DynamicSelectionName = "Balanced" });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.PowerPlan("guid-1", "Balanced"));
    }

    [Fact]
    public async Task PowerPlan_WithoutActivePlan_IsOmitted()
    {
        Arrange(ParityFixtures.PowerPlanSetting(), new SettingStateResult { Success = true, CurrentValue = 0 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task SettingWithNoState_IsOmitted()
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { ParityFixtures.Toggle("t") } });
        _states.Setup(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>())).ReturnsAsync(new Dictionary<string, SettingStateResult>());
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task FeatureOutsideOptimizeAndCustomize_IsSkipped()
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { ["SomethingElse"] = new[] { ParityFixtures.Toggle("t") } });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
        _states.Verify(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()), Times.Never);
    }

    [Fact]
    public async Task Scope_IncludeOtherOsVersions_IsForwardedToTheRegistry()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = true, IsEnabled = false });
        await Sut().CaptureAsync(new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false));
        _registry.Verify(r => r.GetAll(true), Times.Once);
    }
}
